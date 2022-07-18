using System.Diagnostics.CodeAnalysis;

namespace LeasePool;

/// <summary>
/// A pool which can be used to lease objects of type <typeparamref name="T"/>.
///
/// Objects are instantiated as needed and disposed of when no longer needed. 
/// </summary>
/// <typeparam name="T"></typeparam>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedMemberInSuper.Global")]
public interface ILeasePool<T> : IDisposable
{
    /// <summary>
    /// The number of available leases or -1 if infinite.
    /// </summary>
    int AvailableLeases { get; }
    
    /// <summary><inheritdoc cref="LeaseAsync(int, CancellationToken)"/></summary>
    /// <remarks>Will wait forever for a lease.</remarks>
    /// <exception cref="T:System.ObjectDisposedException">The current instance has already been disposed.</exception>
    Task<ILease<T>> LeaseAsync();

    /// <summary><inheritdoc cref="LeaseAsync(int, CancellationToken)"/></summary>
    /// <remarks>Will wait forever for a lease or until the cancellation token is cancelled.</remarks>
    /// <param name="cancellationToken">Token to cancel waiting for a lease.</param>
    /// <exception cref="T:System.ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation of cancelled.</exception>
    Task<ILease<T>> LeaseAsync(CancellationToken cancellationToken);

    /// <summary><inheritdoc cref="LeaseAsync(int, CancellationToken)"/></summary>
    /// <param name="timeout">Time to wait for a lease.</param>
    /// <exception cref="T:System.ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="LeaseTimeoutException">Failed to acquire a lease in <paramref name="timeout"/></exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is a negative number other than -1 milliseconds, which represents an infinite time-out -or- timeout is greater than <see cref="System.Int32.MaxValue"/>.</exception>
    Task<ILease<T>> LeaseAsync(TimeSpan timeout);

    /// <summary><inheritdoc cref="LeaseAsync(int, CancellationToken)"/></summary>
    /// <param name="timeout">Time to wait for a lease.</param>
    /// <param name="cancellationToken">Token to cancel waiting for a lease.</param>
    /// <exception cref="T:System.ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="LeaseTimeoutException">Failed to acquire a lease in <paramref name="timeout"/></exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is a negative number other than -1 milliseconds, which represents an infinite time-out -or- timeout is greater than <see cref="System.Int32.MaxValue"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation of cancelled.</exception>
    Task<ILease<T>> LeaseAsync(TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary><inheritdoc cref="LeaseAsync(int, CancellationToken)"/></summary>
    /// <param name="millisecondsTimeout">Time to wait for a lease.</param>
    /// <exception cref="T:System.ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="LeaseTimeoutException">Failed to acquire a lease in <paramref name="millisecondsTimeout"/></exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="millisecondsTimeout"/> is a negative number other than -1 milliseconds, which represents an infinite time-out -or- timeout is greater than <see cref="System.Int32.MaxValue"/>.</exception>
    Task<ILease<T>> LeaseAsync(int millisecondsTimeout);
    
    /// <summary>
    /// Acquires a lease from the pool, waiting for one to become available if necessary.
    ///
    /// The lease must be disposed after used to return the instance back to the pool.
    /// </summary>
    /// <param name="millisecondsTimeout">Time to wait for a lease.</param>
    /// <param name="cancellationToken">Token to cancel waiting for a lease.</param>
    /// <exception cref="T:System.ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="LeaseTimeoutException">Failed to acquire a lease in <paramref name="millisecondsTimeout"/></exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="millisecondsTimeout"/> is a negative number other than -1 milliseconds, which represents an infinite time-out -or- timeout is greater than <see cref="System.Int32.MaxValue"/>.</exception>
    Task<ILease<T>> LeaseAsync(int millisecondsTimeout, CancellationToken cancellationToken);
    
    /// <summary><inheritdoc cref="LeaseAsync(int, CancellationToken)"/></summary>
    /// <remarks>Will wait forever for a lease.</remarks>
    /// <exception cref="T:System.ObjectDisposedException">The current instance has already been disposed.</exception>
    ILease<T> Lease();

    /// <summary><inheritdoc cref="LeaseAsync(int, CancellationToken)"/></summary>
    /// <param name="timeout">Time to wait for a lease.</param>
    /// <exception cref="T:System.ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="LeaseTimeoutException">Failed to acquire a lease in <paramref name="timeout"/></exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is a negative number other than -1 milliseconds, which represents an infinite time-out -or- timeout is greater than <see cref="System.Int32.MaxValue"/>.</exception>
    ILease<T> Lease(TimeSpan timeout);

    /// <summary><inheritdoc cref="LeaseAsync(int, CancellationToken)"/></summary>
    /// <param name="millisecondsTimeout">Time to wait for a lease.</param>
    /// <exception cref="T:System.ObjectDisposedException">The current instance has already been disposed.</exception>
    /// <exception cref="LeaseTimeoutException">Failed to acquire a lease in <paramref name="millisecondsTimeout"/></exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="millisecondsTimeout"/> is a negative number other than -1 milliseconds, which represents an infinite time-out -or- timeout is greater than <see cref="System.Int32.MaxValue"/>.</exception>
    ILease<T> Lease(int millisecondsTimeout);
}