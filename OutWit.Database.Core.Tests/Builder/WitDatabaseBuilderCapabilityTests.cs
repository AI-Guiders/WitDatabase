using OutWit.Database.Core.Builder;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Storage;

namespace OutWit.Database.Core.Tests.Builder;

/// <summary>
/// Tests for storage capability detection and async build validation.
/// </summary>
[TestFixture]
public class WitDatabaseBuilderCapabilityTests
{
    #region RequiresAsyncBuild Tests

    [Test]
    public void RequiresAsyncBuildReturnsFalseForMemoryStorageTest()
    {
        var builder = new WitDatabaseBuilder()
            .WithMemoryStorage();

        Assert.That(builder.RequiresAsyncBuild(), Is.False);
    }

    [Test]
    public void RequiresAsyncBuildReturnsFalseForFileStorageTest()
    {
        var testPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
        try
        {
            var builder = new WitDatabaseBuilder()
                .WithFilePath(testPath);

            Assert.That(builder.RequiresAsyncBuild(), Is.False);
        }
        finally
        {
            if (File.Exists(testPath))
                File.Delete(testPath);
        }
    }

    [Test]
    public void RequiresAsyncBuildReturnsTrueForAsyncOnlyStorageTest()
    {
        var asyncStorage = new MockAsyncOnlyStorage();
        var builder = new WitDatabaseBuilder()
            .WithStorage(asyncStorage);

        Assert.That(builder.RequiresAsyncBuild(), Is.True);
    }

    [Test]
    public void RequiresAsyncBuildReturnsFalseForCustomStorageWithoutInterfaceTest()
    {
        var regularStorage = new StorageMemory();
        var builder = new WitDatabaseBuilder()
            .WithStorage(regularStorage);

        Assert.That(builder.RequiresAsyncBuild(), Is.False);
    }

    #endregion

    #region SupportsAsyncInitialization Tests

    [Test]
    public void SupportsAsyncInitializationReturnsFalseForMemoryStorageTest()
    {
        var builder = new WitDatabaseBuilder()
            .WithMemoryStorage();

        Assert.That(builder.SupportsAsyncInitialization(), Is.False);
    }

    [Test]
    public void SupportsAsyncInitializationReturnsTrueForAsyncInitializableStorageTest()
    {
        var asyncStorage = new MockAsyncOnlyStorage();
        var builder = new WitDatabaseBuilder()
            .WithStorage(asyncStorage);

        Assert.That(builder.SupportsAsyncInitialization(), Is.True);
    }

    #endregion

    #region GetStorageProviderKey Tests

    [Test]
    public void GetStorageProviderKeyReturnsNullWhenNoStorageConfiguredTest()
    {
        var builder = new WitDatabaseBuilder();

        Assert.That(builder.GetStorageProviderKey(), Is.Null);
    }

    [Test]
    public void GetStorageProviderKeyReturnsCorrectKeyForMemoryStorageTest()
    {
        var storage = new StorageMemory();
        var builder = new WitDatabaseBuilder()
            .WithStorage(storage);

        Assert.That(builder.GetStorageProviderKey(), Is.EqualTo("memory"));
    }

    #endregion

    #region Build Validation Tests

    [Test]
    public void BuildThrowsForAsyncOnlyStorageTest()
    {
        var asyncStorage = new MockAsyncOnlyStorage();
        var builder = new WitDatabaseBuilder()
            .WithStorage(asyncStorage);

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.That(ex!.Message, Does.Contain("requires asynchronous operations"));
        Assert.That(ex.Message, Does.Contain("BuildAsync"));
    }

    [Test]
    public void BuildStoreThrowsForAsyncOnlyStorageTest()
    {
        var asyncStorage = new MockAsyncOnlyStorage();
        var builder = new WitDatabaseBuilder()
            .WithStorage(asyncStorage);

        var ex = Assert.Throws<InvalidOperationException>(() => builder.BuildStore());
        Assert.That(ex!.Message, Does.Contain("requires asynchronous operations"));
    }

    [Test]
    public void BuildTransactionalStoreThrowsForAsyncOnlyStorageTest()
    {
        var asyncStorage = new MockAsyncOnlyStorage();
        var builder = new WitDatabaseBuilder()
            .WithStorage(asyncStorage);

        var ex = Assert.Throws<InvalidOperationException>(() => builder.BuildTransactionalStore());
        Assert.That(ex!.Message, Does.Contain("requires asynchronous operations"));
    }

    [Test]
    public async Task BuildAsyncWorksForAsyncOnlyStorageTest()
    {
        var asyncStorage = new MockAsyncOnlyStorage();
        var builder = new WitDatabaseBuilder()
            .WithStorage(asyncStorage)
            .WithoutFileLocking();

        // Should not throw
        await using var db = await builder.BuildAsync();
        Assert.That(db, Is.Not.Null);
    }

    [Test]
    public void BuildWorksForRegularStorageTest()
    {
        var builder = new WitDatabaseBuilder()
            .WithMemoryStorage()
            .WithoutFileLocking();

        // Should not throw
        using var db = builder.Build();
        Assert.That(db, Is.Not.Null);
    }

    #endregion

    #region Mock Classes

    /// <summary>
    /// Mock storage that implements IAsyncOnlyStorage for testing.
    /// </summary>
    private sealed class MockAsyncOnlyStorage : IAsyncOnlyStorage, IAsyncInitializable
    {
        private readonly StorageMemory m_inner = new();
        private bool m_initialized;

        public bool RequiresAsyncOperations => true;

        public bool IsInitialized => m_initialized;

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            m_initialized = true;
            return ValueTask.CompletedTask;
        }

        public void ReadPage(long pageNumber, Span<byte> buffer) => m_inner.ReadPage(pageNumber, buffer);
        
        public ValueTask ReadPageAsync(long pageNumber, Memory<byte> buffer, CancellationToken cancellationToken = default) 
            => m_inner.ReadPageAsync(pageNumber, buffer, cancellationToken);

        public void WritePage(long pageNumber, ReadOnlySpan<byte> buffer) => m_inner.WritePage(pageNumber, buffer);
        
        public ValueTask WritePageAsync(long pageNumber, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => m_inner.WritePageAsync(pageNumber, buffer, cancellationToken);

        public void Flush() => m_inner.Flush();
        
        public ValueTask FlushAsync(CancellationToken cancellationToken = default) 
            => m_inner.FlushAsync(cancellationToken);

        public void SetSize(long pageCount) => m_inner.SetSize(pageCount);
        
        public ValueTask SetSizeAsync(long pageCount, CancellationToken cancellationToken = default)
        {
            m_inner.SetSize(pageCount);
            return ValueTask.CompletedTask;
        }

        public int PageSize => m_inner.PageSize;
        public long PageCount => m_inner.PageCount;
        public bool IsReadOnly => false;
        public string ProviderKey => "mock-async";

        public void Dispose() => m_inner.Dispose();
    }

    #endregion
}
