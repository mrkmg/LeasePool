namespace LeasePool;

/// <summary>
/// Represents a leased item. Make sure to call Dispose() when you are done with it.
/// </summary>
/// <typeparam name="T"></typeparam>
public interface ILease<out T> : IDisposable
{
    /// <summary>
    /// The leased item.
    /// </summary>
    T Value { get; }
}