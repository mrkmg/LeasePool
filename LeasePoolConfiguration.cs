using System.Diagnostics.CodeAnalysis;

namespace LeasePool;

/// <summary>
/// Configuration for the LeasePool.
/// </summary>
/// <typeparam name="T"></typeparam>
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class LeasePoolConfiguration<T>
{
    /// <summary>
    /// The maximum number of total instance of T that can be leased or idle in the pool.
    /// </summary>
    public int MaxLeases { get; set; } = -1;

    /// <summary>
    /// <para>How long an instance of T can be idle in the pool before it is automatically disposed.</para>
    /// <para>If set to zero, objects are never kept in a pool, and are disposed immediately when returned to the pool.</para>
    /// </summary>
    public int IdleTimeout { get; set; } = -1;
    
    /// <summary>
    /// A factory method that creates an instance of T.
    /// </summary>
    /// <remarks>By default, this calls <see cref="Activator.CreateInstance&lt;T&gt;()" /></remarks>
    public Func<T>? Initializer { get; set; }
    
    /// <summary>
    /// <para>Validates an instance of T before it is leased from the pool.</para>
    /// <para>If this returns false, the instance will be disposed and a new instance will be created.</para>
    /// </summary>
    /// <remarks>By default, this always returns true.</remarks>
    public Func<T, bool>? Validator { get; set; }
    
    /// <summary>
    /// A factory method that is called when an instance of T is to
    /// be disposed.
    /// </summary>
    /// <remarks>By default, this method checks if the instance is an IDisposable
    /// and calls Dispose() on it.</remarks>
    public Action<T>? Finalizer { get; set; }
    
    /// <summary>
    /// Is executed on an object before it is leased.
    /// </summary>
    /// <remarks>Does nothing by default.</remarks>
    public Action<T>? OnLease { get; set; }
    
    /// <summary>
    /// Is executed on a leased object before it is returned to the pool.
    /// </summary>
    /// <remarks>Does nothing by default.</remarks>
    public Action<T>? OnReturn { get; set; }
}