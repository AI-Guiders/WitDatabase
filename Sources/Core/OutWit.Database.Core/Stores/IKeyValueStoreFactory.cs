using OutWit.Database.Core.Interfaces;

namespace OutWit.Database.Core.Stores;

/// <summary>
/// Factory interface for creating key-value stores.
/// Used to create stores for secondary indexes with the same configuration as the main store.
/// </summary>
public interface IKeyValueStoreFactory
{
    /// <summary>
    /// Creates a new key-value store with the given name/path.
    /// </summary>
    /// <param name="name">Name or relative path for the store.</param>
    /// <returns>A new key-value store instance.</returns>
    IKeyValueStore Create(string name);
    
    /// <summary>
    /// Gets the provider key for stores created by this factory.
    /// </summary>
    string ProviderKey { get; }
}
