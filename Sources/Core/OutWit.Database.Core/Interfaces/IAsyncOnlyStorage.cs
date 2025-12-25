namespace OutWit.Database.Core.Interfaces;

/// <summary>
/// Marker interface for storage implementations that require asynchronous initialization
/// and operations. Used to detect storage that cannot use synchronous I/O.
/// </summary>
/// <remarks>
/// Implementations of this interface (e.g., IndexedDB for Blazor WASM) should:
/// <list type="bullet">
///   <item>Throw <see cref="PlatformNotSupportedException"/> from sync methods</item>
///   <item>Implement <see cref="IAsyncInitializable"/> if async initialization is required</item>
///   <item>Only work with async database creation via <see cref="Builder.WitDatabaseBuilder.BuildAsync"/></item>
/// </list>
/// </remarks>
public interface IAsyncOnlyStorage : IStorage
{
    /// <summary>
    /// Gets whether this storage requires async-only operations.
    /// When true, synchronous operations will throw <see cref="PlatformNotSupportedException"/>.
    /// </summary>
    /// <remarks>
    /// This is primarily used for Blazor WebAssembly with IndexedDB storage,
    /// where synchronous I/O is not available due to the single-threaded nature of WASM.
    /// </remarks>
    bool RequiresAsyncOperations { get; }
}
