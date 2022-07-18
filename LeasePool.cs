using System.Diagnostics.CodeAnalysis;
using Timer = System.Timers.Timer;

namespace LeasePool;

/// <inheritdoc />
[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class LeasePool<T> : ILeasePool<T> where T : class
{
    protected readonly LeasePoolConfiguration<T> Configuration;
    private readonly Queue<LeasePoolItem> _objects;
    private readonly SemaphoreSlim? _semaphore;
    private readonly Timer? _timer;
    private readonly object _lock = new();
    private bool _isDisposed;

    public LeasePool(int? maxSize = null,
                     int? idleTimeout = null,
                     Func<T>? initializer = null,
                     Action<T>? finalizer = null,
                     Func<T, bool>? validator = null,
                     Action<T>? onLease = null,
                     Action<T>? onReturn = null) : this(new()
    {
        MaxSize = maxSize ?? LeasePoolConfiguration<T>.DefaultMaxCount,
        IdleTimeout = idleTimeout ?? LeasePoolConfiguration<T>.DefaultIdleTimeout,
        Initializer = initializer ?? LeasePoolConfiguration<T>.DefaultInitializer,
        Finalizer = finalizer ?? LeasePoolConfiguration<T>.DefaultFinalizer,
        Validator = validator ?? LeasePoolConfiguration<T>.DefaultValidator,
        OnLease = onLease ?? LeasePoolConfiguration<T>.DefaultOnLease,
        OnReturn = onReturn ?? LeasePoolConfiguration<T>.DefaultOnReturn
    }) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="LeasePool{T}"/> class.
    /// </summary>
    /// <param name="configuration"></param>
    public LeasePool(LeasePoolConfiguration<T> configuration)
    {
        Configuration = configuration;
        
        if (Configuration.MaxSize is < -1 or 0)
            throw new ArgumentException("Must be greater than 0 or -1", nameof(Configuration.MaxSize));
        
        if (Configuration.IdleTimeout is < -1 or 0)
            throw new ArgumentException("Must be greater than 0 or -1", nameof(Configuration.IdleTimeout));
        
        _objects = new();

        if (Configuration.IdleTimeout > 0)
        {
            _timer = new(Configuration.IdleTimeout);
            _timer.Elapsed += (_, _) => Cleanup();
        }
        
        if (Configuration.MaxSize > 0)
            _semaphore = new(Configuration.MaxSize);
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
                if (Configuration.Validator(o.Object))
                {
                    obj = o.Object;
                    break;
                }
                Configuration.Finalizer(o.Object);
            }
        }
        obj ??= Configuration.Initializer();
        Configuration.OnLease(obj);
        return new ActiveLease(this,  obj);
    }
    
    private void Return(ActiveLease obj)
    {
        if (_isDisposed)
            return;

        Configuration.OnReturn(obj.Value);
        
        // If IdleTimeout is zero, we immediately finalize the object.
        if (Configuration.IdleTimeout == 0)
        {
            Configuration.Finalizer(obj.Value);
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
            while (_objects.TryPeek(out var item) && item.LastUsed.AddMilliseconds(Configuration.IdleTimeout) <= DateTime.UtcNow)
            {
                _objects.Dequeue();
                Configuration.Finalizer(item.Object);
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
            Configuration.Finalizer(obj.Object);
        
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