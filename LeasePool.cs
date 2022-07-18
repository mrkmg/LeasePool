using System.Diagnostics.CodeAnalysis;
using Timer = System.Timers.Timer;

namespace LeasePool;

/// <inheritdoc />
[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class LeasePool<T> : ILeasePool<T> where T : class
{
    private static readonly Func<T> DefaultInitializer = Activator.CreateInstance<T>;
    private static readonly Action<T> DefaultFinalizer = t =>
    {
        if (t is IDisposable disposable) disposable.Dispose();
    };

    /// <inheritdoc />
    public int AvailableLeases => MaxLeases - _leasesSemaphore?.CurrentCount ?? -1;

    protected readonly int MaxLeases;
    protected readonly int IdleTimeout;

    protected Func<T> Initializer { get; set; }
    protected Func<T, bool>? Validator { get; set; }
    protected Action<T> Finalizer { get; set; }
    protected Action<T>? OnLease { get; set; }
    protected Action<T>? OnReturn { get; set; }
    
    private readonly Queue<LeasePoolItem> _objects;
    private readonly SemaphoreSlim? _leasesSemaphore;
    private readonly SemaphoreSlim _queueSemaphore;
    private readonly Timer? _timer;
    
    private bool _isDisposed;

    /// <summary>
    /// Create a new LeasePool for the given type.
    /// </summary>
    /// <param name="maxLeases">The maximum number of total instance of T that can be leased or idle in the pool.</param>
    /// <param name="idleTimeout">
    ///     <para>How long an instance of T can be idle in the pool before it is automatically disposed.</para>
    ///     <para>If set to zero, objects are never kept in a pool, and are disposed immediately when returned to the pool.</para>
    /// </param>
    /// <param name="initializer">
    ///     <para>A factory method that creates an instance of T.</para>
    ///     <para>By default, this calls <see cref="Activator.CreateInstance&lt;T&gt;()" /></para>
    /// </param>
    /// <param name="finalizer">
    ///     <para>Validates an instance of T before it is leased from the pool.</para>
    ///     <para>If this returns false, the instance will be disposed and a new instance will be created.</para>
    /// </param>
    /// <param name="validator">
    ///     <para>A factory method that is called when an instance of T is to be disposed.</para>
    ///     <para>By default, this method checks if the instance is an IDisposable and calls Dispose() on it.</para>
    /// </param>
    /// <param name="onLease">Is executed on an object before it is leased.</param>
    /// <param name="onReturn">Does nothing by default.</param>
    /// <exception cref="ArgumentException"> If maxLeases is 0 or less than -1 (-1 means infinite)</exception>
    /// <exception cref="ArgumentException"> If idleTimeout is 0 or less than -1 (-1 means infinite)</exception>
    public LeasePool(int maxLeases = -1, int idleTimeout = -1, 
                     Func<T>? initializer = null, Action<T>? finalizer = null, 
                     Func<T, bool>? validator = null, 
                     Action<T>? onLease = null, Action<T>? onReturn = null)
    {
        MaxLeases = maxLeases;
        IdleTimeout = idleTimeout;
        Initializer = initializer ?? DefaultInitializer;
        Finalizer = finalizer ?? DefaultFinalizer;
        Validator = validator;
        OnLease = onLease;
        OnReturn = onReturn;
        
        if (maxLeases is < -1 or 0)
            throw new ArgumentException("Must be greater than 0 or equal to -1", nameof(maxLeases));
        
        if (idleTimeout is < -1 or 0)
            throw new ArgumentException("Must be greater than 0 or equal to -1", nameof(idleTimeout));
        
        _objects = new(maxLeases > 0 ? maxLeases : 0);
        _queueSemaphore = new(1, 1);
        
        if (IdleTimeout > 0)
        {
            _timer = new(idleTimeout);
            _timer.Elapsed += (_, _) => Cleanup();
        }
        
        if (maxLeases > 0)
            _leasesSemaphore = new(maxLeases, maxLeases);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LeasePool{T}"/> class.
    /// </summary>
    /// <param name="configuration"></param>
    public LeasePool(LeasePoolConfiguration<T> configuration) : this(
        configuration.MaxLeases,
        configuration.IdleTimeout,
        configuration.Initializer ?? DefaultInitializer,
        configuration.Finalizer ?? DefaultFinalizer,
        configuration.Validator,
        configuration.OnLease,
        configuration.OnReturn) { }

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
    public async Task<ILease<T>> Lease(int millisecondsTimeout, CancellationToken token)
    {
        if (millisecondsTimeout < -1)
            throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout));
        
        if (_isDisposed) throw new ObjectDisposedException(nameof(LeasePool<T>));
        
        var start = Environment.TickCount;
        
        if (_leasesSemaphore is not null)
            await Waiter(_leasesSemaphore, start, millisecondsTimeout, token).ConfigureAwait(false);
        
        T obj;
        while (true)
        {
            await Waiter(_queueSemaphore, start, millisecondsTimeout, token).ConfigureAwait(false);
            var didRetrieve = _objects.TryDequeue(out var o);
            _queueSemaphore.Release();
            
            if (!didRetrieve)
            {
                obj = Initializer();
                break;
            }
            
            if (Validator?.Invoke(o.Object) ?? true)
            {
                obj = o.Object;
                break;
            }
            Finalizer(o.Object);
        }
        OnLease?.Invoke(obj);
        return new ActiveLease(this,  obj);
    }
    
    private void Return(ActiveLease obj)
    {
        if (_isDisposed)
            return;

        OnReturn?.Invoke(obj.Value);
        
        // If IdleTimeout is zero, immediately finalize the object.
        if (IdleTimeout == 0)
        {
            Finalizer(obj.Value);
            _leasesSemaphore?.Release();
            return;
        }

        _queueSemaphore.Wait();
        _objects.Enqueue(new(obj.Value));
        _queueSemaphore.Release();
        _leasesSemaphore?.Release();
        if (_timer is not null && !_timer.Enabled)
            _timer.Start();
    }

    private void Cleanup()
    {
        while (true)
        {
            _queueSemaphore.Wait();
            var didRetrieve = _objects.TryPeek(out var item);
            if (!didRetrieve || item.LastUsed.AddMilliseconds(IdleTimeout) > DateTime.Now)
            {
                _queueSemaphore.Release();
                break;
            }
            // We will get the same object as the TryPeek() above because of the _queueSemaphore locking
            _objects.Dequeue();
            _queueSemaphore.Release();
            Finalizer(item.Object);
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
        _queueSemaphore.Dispose();
        _leasesSemaphore?.Dispose();
        _timer?.Dispose();
        
        foreach (var obj in _objects)
            Finalizer(obj.Object);
        
        GC.SuppressFinalize(this);
    }

    private static Task<bool> Waiter(SemaphoreSlim sem, int start, int timeout, CancellationToken innerToken)
    {
        if (timeout is -1 or 0) return sem.WaitAsync(timeout, innerToken);
        var remaining = timeout - Environment.TickCount - start;
        if (remaining < 0) 
            throw new LeaseTimeoutException($"Timeout of {timeout}ms exceeded");
        return sem.WaitAsync(remaining, innerToken);
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