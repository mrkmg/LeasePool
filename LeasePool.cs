using System.Diagnostics.CodeAnalysis;
using Timer = System.Timers.Timer;

namespace LeasePool;

/// <inheritdoc />
[SuppressMessage("ReSharper", "UnusedType.Global")]
public class LeasePool<T> : ILeasePool<T> where T : class
{
    private readonly Queue<LeasePoolItem> _objects;
    private readonly LeasePoolConfiguration<T> _configuration;
    private readonly SemaphoreSlim? _semaphore;
    private readonly Timer? _timer;
    private readonly object _lock = new();
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeasePool{T}"/> class.
    /// </summary>
    /// <param name="configuration"></param>
    public LeasePool(LeasePoolConfiguration<T>? configuration = null)
    {
        _configuration = configuration ?? new LeasePoolConfiguration<T>();
        
        if (_configuration.MaxSize is < -1 or 0)
            throw new ArgumentException("Must be greater than 0 or -1", nameof(_configuration.MaxSize));
        
        if (_configuration.IdleTimeout is < -1 or 0)
            throw new ArgumentException("Must be greater than 0 or -1", nameof(_configuration.IdleTimeout));
        
        _objects = new();

        if (_configuration.IdleTimeout > 0)
        {
            _timer = new(_configuration.IdleTimeout);
            _timer.Elapsed += (_, _) => Cleanup();
        }
        
        if (_configuration.MaxSize > 0)
            _semaphore = new(_configuration.MaxSize);
    }
    
    /// <inheritdoc />
    public Task<ILease<T>> Lease() => Lease(Timeout.Infinite, CancellationToken.None);
    
    /// <inheritdoc />
    public Task<ILease<T>> Lease(CancellationToken cancellationToken) => Lease(Timeout.Infinite, cancellationToken);
    
    /// <inheritdoc />
    public Task<ILease<T>> Lease(TimeSpan timeout) => Lease(timeout, CancellationToken.None);
    
    /// <inheritdoc />
    public Task<ILease<T>> Lease(TimeSpan timeout, CancellationToken cancellationToken) 
        => Lease(
            timeout.TotalMilliseconds is > -1 and <= int.MaxValue 
                ? (int)timeout.TotalMilliseconds 
                : throw new ArgumentOutOfRangeException(nameof(timeout)), 
            cancellationToken);
    
    /// <inheritdoc />
    public Task<ILease<T>> Lease(int millisecondsTimeout) => Lease(millisecondsTimeout, CancellationToken.None);
    
    /// <inheritdoc />
    public async Task<ILease<T>> Lease(int timeout, CancellationToken token)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(LeasePool<T>));

        if (_semaphore is not null)
            if (!await _semaphore.WaitAsync(timeout, token).ConfigureAwait(false))
                throw new LeaseTimeoutException($"Timeout of {timeout}ms exceeded");
        
        T? obj = null;
        lock (_lock)
        {
            while (_objects.TryDequeue(out var o))
            {
                if (_configuration.Validator(o.Object))
                {
                    obj = o.Object;
                    break;
                }
                _configuration.Finalizer(o.Object);
            }
        }
        obj ??= _configuration.Initializer();
        _configuration.OnLease(obj);
        return new ActiveLease(this,  obj);
    }
    
    private void Return(ActiveLease obj)
    {
        if (_isDisposed)
            return;

        _configuration.OnReturn(obj.Value);
        
        // If IdleTimeout is zero, we immediately finalize the object.
        if (_configuration.IdleTimeout == 0)
        {
            _configuration.Finalizer(obj.Value);
            _semaphore?.Release();
            return;
        }
        
        lock (_lock)
        {
            _objects.Enqueue(new(obj.Value));
        }
        _semaphore?.Release();
        if (_timer is not null && !_timer.Enabled)
            _timer.Start();
    }

    private void Cleanup()
    {
        lock (_lock)
        {
            var didRemove = false;
            while (_objects.TryPeek(out var item) && item.LastUsed.AddMilliseconds(_configuration.IdleTimeout) <= DateTime.UtcNow)
            {
                _objects.Dequeue();
                _configuration.Finalizer(item.Object);
                didRemove = true;
            }
            if (didRemove) _timer?.Start();
        }
    }

    /// <summary>
    /// Disposes the pool, and all objects in it.
    /// </summary>
    /// <exception cref="ObjectDisposedException"></exception>
    public void Dispose()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(LeasePool<T>));
        
        _isDisposed = true;
        _semaphore?.Dispose();
        _timer?.Dispose();
        
        foreach (var obj in _objects)
            _configuration.Finalizer(obj.Object);
        
        GC.SuppressFinalize(this);
    }

    private readonly struct ActiveLease : ILease<T>
    {
        public T Value { get; }
        private readonly LeasePool<T> _pool;
        
        public ActiveLease(LeasePool<T> pool, T value)
        {
            _pool = pool;
            Value = value;
        }
        
        public void Dispose()
        {
            _pool.Return(this);
        }
    }

    private struct LeasePoolItem
    {
        public readonly T Object;
        public readonly DateTime LastUsed;
        
        public LeasePoolItem(T obj)
        {
            Object = obj;
            LastUsed = DateTime.UtcNow;
        }
    }
}

/// <summary>
/// The exception that is thrown when a lease times out.
/// </summary>
public class LeaseTimeoutException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LeaseTimeoutException"/> class.
    /// </summary>
    /// <param name="message"></param>
    public LeaseTimeoutException(string message) : base(message) { }
}