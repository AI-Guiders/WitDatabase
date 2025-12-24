namespace OutWit.Database.Core.Interfaces;

/// <summary>
/// Interface for components that require asynchronous initialization.
/// Used primarily for Blazor WebAssembly scenarios where synchronous I/O is not available.
/// </summary>
/// <remarks>
/// Implementations should:
/// - Be safe to call multiple times (idempotent)
/// - Track initialization state via <see cref="IsInitialized"/>
/// - Throw if used before initialization when sync-only operations are attempted
/// </remarks>
public interface IAsyncInitializable
{
    /// <summary>
    /// Initializes the component asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// This method should be idempotent - calling it multiple times should be safe.
    /// </remarks>
    ValueTask InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets whether the component has been initialized.
    /// </summary>
    bool IsInitialized { get; }
}
