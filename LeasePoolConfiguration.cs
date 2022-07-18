namespace LeasePool;

/// <summary>
/// Configuration for the LeasePool.
/// </summary>
/// <typeparam name="T"></typeparam>
public class LeasePoolConfiguration<T>
{
    /// <summary>
    /// The maximum number of total instance of T that can be leased or idle in the pool.
    /// </summary>
    public int MaxSize { get; set; } = -1;
    
    /// <summary>
    /// How long an instance of T can be idle in the pool before it is automatically disposed.
    /// <br /><br />
    /// If set to zero, objects are never kept in a pool, and are disposed immediately
    /// when returned to the pool.
    /// </summary>
    public int IdleTimeout { get; set; } = -1;
    
    /// <summary>
    /// A factory method that creates an instance of T.
    /// </summary>
    /// <remarks>By default, this calls <see cref="Activator.CreateInstance&lt;T&gt;()" /></remarks>
    public Func<T> Initializer { get; set; } = DefaultInitializer;
    
    /// <summary>
    /// Validates an instance of T before it is leased from the pool.
    ///<br /><br />
    /// If this returns false, the instance will be disposed and a
    /// new instance will be created.
    /// </summary>
    /// <remarks>By default, this always returns true.</remarks>
    public Func<T, bool> Validator { get; set; } = DefaultValidator;
    
    /// <summary>
    /// A factory method that is called when an instance of T is to
    /// be disposed.
    /// </summary>
    /// <remarks>By default, this method checks if the instance is an IDisposable
    /// and calls Dispose() on it.</remarks>
    public Action<T> Finalizer { get; set; } = DefaultFinalizer;
    
    /// <summary>
    /// Is executed on an object before it is leased.
    /// </summary>
    /// <remarks>Does nothing by default.</remarks>
    public Action<T> OnLease { get; set; } = DefaultOnLease;
    
    /// <summary>
    /// Is executed on a leased object before it is returned to the pool.
    /// </summary>
    /// <remarks>Does nothing by default.</remarks>
    public Action<T> OnReturn { get; set; } = DefaultOnReturn;

    private static readonly Func<T> DefaultInitializer = Activator.CreateInstance<T>;
    private static readonly Func<T, bool> DefaultValidator = _ => true;
    private static readonly Action<T> DefaultOnReturn = _ => { };
    private static readonly Action<T> DefaultOnLease = _ => { };
    private static readonly Action<T> DefaultFinalizer = t =>
    {
        if (t is IDisposable disposable) disposable.Dispose();
    };

}