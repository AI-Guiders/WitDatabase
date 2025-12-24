using NUnit.Framework;
using OutWit.Database.Core.IndexedDb.Indexes;
using OutWit.Database.Core.IndexedDb.Tests.Mocks;

namespace OutWit.Database.Core.IndexedDb.Tests.Indexes;

[TestFixture]
public class IndexedDbIndexInteropTests
{
    #region Fields

    private MockJSRuntime m_jsRuntime = null!;

    #endregion

    #region Setup

    [SetUp]
    public void Setup()
    {
        m_jsRuntime = new MockJSRuntime();
    }

    #endregion

    #region Constructor Tests

    [Test]
    public void ConstructorWithValidParametersSucceedsTest()
    {
        // Act
        var interop = new IndexedDbIndexInterop(m_jsRuntime, "TestDb", "TestStore");

        // Assert
        Assert.That(interop, Is.Not.Null);
        Assert.That(interop.DatabaseName, Is.EqualTo("TestDb"));
        Assert.That(interop.StoreName, Is.EqualTo("TestStore"));
    }

    [Test]
    public void ConstructorWithNullJsRuntimeThrowsTest()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new IndexedDbIndexInterop(null!, "TestDb", "TestStore"));
    }

    [Test]
    public void ConstructorWithNullDatabaseNameThrowsTest()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            new IndexedDbIndexInterop(m_jsRuntime, null!, "TestStore"));
    }

    [Test]
    public void ConstructorWithEmptyDatabaseNameThrowsTest()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            new IndexedDbIndexInterop(m_jsRuntime, "", "TestStore"));
    }

    [Test]
    public void ConstructorWithNullStoreNameThrowsTest()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            new IndexedDbIndexInterop(m_jsRuntime, "TestDb", null!));
    }

    [Test]
    public void ConstructorWithEmptyStoreNameThrowsTest()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            new IndexedDbIndexInterop(m_jsRuntime, "TestDb", ""));
    }

    #endregion

    #region Open/Close Tests

    [Test]
    public async Task OpenAsyncSucceedsTest()
    {
        // Arrange
        await using var interop = new IndexedDbIndexInterop(m_jsRuntime, "TestDb", "TestStore");

        // Act & Assert
        await interop.OpenAsync();
        Assert.That(m_jsRuntime.Databases.ContainsKey("TestDb"), Is.True);
    }

    [Test]
    public async Task CloseAsyncSucceedsTest()
    {
        // Arrange
        await using var interop = new IndexedDbIndexInterop(m_jsRuntime, "TestDb", "TestStore");
        await interop.OpenAsync();

        // Act & Assert
        await interop.CloseAsync();
    }

    #endregion

    #region Get/Put Tests

    [Test]
    public async Task PutAndGetReturnsValueTest()
    {
        // Arrange
        await using var interop = new IndexedDbIndexInterop(m_jsRuntime, "TestDb", "TestStore");
        await interop.OpenAsync();
        var key = GetBytes("key1");
        var value = GetBytes("value1");

        // Act
        await interop.PutAsync(key, value);
        var result = await interop.GetAsync(key);

        // Assert
        Assert.That(result, Is.EqualTo(value));
    }

    [Test]
    public async Task GetNonExistingKeyReturnsNullTest()
    {
        // Arrange
        await using var interop = new IndexedDbIndexInterop(m_jsRuntime, "TestDb", "TestStore");
        await interop.OpenAsync();

        // Act
        var result = await interop.GetAsync(GetBytes("nonexistent"));

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task PutOverwritesExistingValueTest()
    {
        // Arrange
        await using var interop = new IndexedDbIndexInterop(m_jsRuntime, "TestDb", "TestStore");
        await interop.OpenAsync();
        var key = GetBytes("key1");

        // Act
        await interop.PutAsync(key, GetBytes("value1"));
        await interop.PutAsync(key, GetBytes("value2"));
        var result = await interop.GetAsync(key);

        // Assert
        Assert.That(result, Is.EqualTo(GetBytes("value2")));
    }

    #endregion

    #region Delete Tests

    [Test]
    public async Task DeleteExistingKeyReturnsTrueTest()
    {
        // Arrange
        await using var interop = new IndexedDbIndexInterop(m_jsRuntime, "TestDb", "TestStore");
        await interop.OpenAsync();
        var key = GetBytes("key1");
        await interop.PutAsync(key, GetBytes("value1"));

        // Act
        var result = await interop.DeleteAsync(key);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(await interop.GetAsync(key), Is.Null);
    }

    [Test]
    public async Task DeleteNonExistingKeyReturnsFalseTest()
    {
        // Arrange
        await using var interop = new IndexedDbIndexInterop(m_jsRuntime, "TestDb", "TestStore");
        await interop.OpenAsync();

        // Act
        var result = await interop.DeleteAsync(GetBytes("nonexistent"));

        // Assert
        Assert.That(result, Is.False);
    }

    #endregion

    #region DeleteRange Tests

    [Test]
    public async Task DeleteRangeDeletesEntriesInRangeTest()
    {
        // Arrange
        await using var interop = new IndexedDbIndexInterop(m_jsRuntime, "TestDb", "TestStore");
        await interop.OpenAsync();
        await interop.PutAsync(GetBytes("a"), GetBytes("1"));
        await interop.PutAsync(GetBytes("b"), GetBytes("2"));
        await interop.PutAsync(GetBytes("c"), GetBytes("3"));
        await interop.PutAsync(GetBytes("d"), GetBytes("4"));

        // Act
        var count = await interop.DeleteRangeAsync(GetBytes("b"), GetBytes("d"));

        // Assert
        Assert.That(count, Is.EqualTo(2));
        Assert.That(await interop.GetAsync(GetBytes("a")), Is.Not.Null);
        Assert.That(await interop.GetAsync(GetBytes("b")), Is.Null);
        Assert.That(await interop.GetAsync(GetBytes("c")), Is.Null);
        Assert.That(await interop.GetAsync(GetBytes("d")), Is.Not.Null);
    }

    #endregion

    #region Scan Tests

    [Test]
    public async Task ScanReturnsAllEntriesWhenNoRangeTest()
    {
        // Arrange
        await using var interop = new IndexedDbIndexInterop(m_jsRuntime, "TestDb", "TestStore");
        await interop.OpenAsync();
        await interop.PutAsync(GetBytes("a"), GetBytes("1"));
        await interop.PutAsync(GetBytes("b"), GetBytes("2"));
        await interop.PutAsync(GetBytes("c"), GetBytes("3"));

        // Act
        var results = new List<(byte[] Key, byte[] Value)>();
        await foreach (var entry in interop.ScanAsync(null, null))
        {
            results.Add(entry);
        }

        // Assert
        Assert.That(results, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task ScanReturnsEntriesInRangeTest()
    {
        // Arrange
        await using var interop = new IndexedDbIndexInterop(m_jsRuntime, "TestDb", "TestStore");
        await interop.OpenAsync();
        await interop.PutAsync(GetBytes("a"), GetBytes("1"));
        await interop.PutAsync(GetBytes("b"), GetBytes("2"));
        await interop.PutAsync(GetBytes("c"), GetBytes("3"));
        await interop.PutAsync(GetBytes("d"), GetBytes("4"));

        // Act
        var results = new List<(byte[] Key, byte[] Value)>();
        await foreach (var entry in interop.ScanAsync(GetBytes("b"), GetBytes("d")))
        {
            results.Add(entry);
        }

        // Assert
        Assert.That(results, Has.Count.EqualTo(2));
    }

    #endregion

    #region HasAny Tests

    [Test]
    public async Task HasAnyReturnsTrueWhenEntriesExistTest()
    {
        // Arrange
        await using var interop = new IndexedDbIndexInterop(m_jsRuntime, "TestDb", "TestStore");
        await interop.OpenAsync();
        await interop.PutAsync(GetBytes("a"), GetBytes("1"));
        await interop.PutAsync(GetBytes("b"), GetBytes("2"));

        // Act
        var result = await interop.HasAnyAsync(GetBytes("a"), GetBytes("c"));

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task HasAnyReturnsFalseWhenNoEntriesTest()
    {
        // Arrange
        await using var interop = new IndexedDbIndexInterop(m_jsRuntime, "TestDb", "TestStore");
        await interop.OpenAsync();
        await interop.PutAsync(GetBytes("a"), GetBytes("1"));
        await interop.PutAsync(GetBytes("z"), GetBytes("2"));

        // Act
        var result = await interop.HasAnyAsync(GetBytes("b"), GetBytes("d"));

        // Assert
        Assert.That(result, Is.False);
    }

    #endregion

    #region Count Tests

    [Test]
    public async Task CountReturnsCorrectCountTest()
    {
        // Arrange
        await using var interop = new IndexedDbIndexInterop(m_jsRuntime, "TestDb", "TestStore");
        await interop.OpenAsync();
        await interop.PutAsync(GetBytes("a"), GetBytes("1"));
        await interop.PutAsync(GetBytes("b"), GetBytes("2"));
        await interop.PutAsync(GetBytes("c"), GetBytes("3"));

        // Act
        var count = await interop.CountAsync();

        // Assert
        Assert.That(count, Is.EqualTo(3));
    }

    [Test]
    public async Task CountReturnsZeroForEmptyStoreTest()
    {
        // Arrange
        await using var interop = new IndexedDbIndexInterop(m_jsRuntime, "TestDb", "TestStore");
        await interop.OpenAsync();

        // Act
        var count = await interop.CountAsync();

        // Assert
        Assert.That(count, Is.EqualTo(0));
    }

    #endregion

    #region Clear Tests

    [Test]
    public async Task ClearRemovesAllEntriesTest()
    {
        // Arrange
        await using var interop = new IndexedDbIndexInterop(m_jsRuntime, "TestDb", "TestStore");
        await interop.OpenAsync();
        await interop.PutAsync(GetBytes("a"), GetBytes("1"));
        await interop.PutAsync(GetBytes("b"), GetBytes("2"));
        await interop.PutAsync(GetBytes("c"), GetBytes("3"));

        // Act
        await interop.ClearAsync();

        // Assert
        Assert.That(await interop.CountAsync(), Is.EqualTo(0));
    }

    #endregion

    #region Dispose Tests

    [Test]
    public async Task DisposeAsyncSucceedsTest()
    {
        // Arrange
        var interop = new IndexedDbIndexInterop(m_jsRuntime, "TestDb", "TestStore");
        await interop.OpenAsync();

        // Act & Assert
        await interop.DisposeAsync();
    }

    [Test]
    public async Task DoubleDisposeAsyncDoesNotThrowTest()
    {
        // Arrange
        var interop = new IndexedDbIndexInterop(m_jsRuntime, "TestDb", "TestStore");
        await interop.OpenAsync();
        await interop.DisposeAsync();

        // Act & Assert
        await interop.DisposeAsync();
    }

    [Test]
    public async Task AccessAfterDisposeThrowsTest()
    {
        // Arrange
        var interop = new IndexedDbIndexInterop(m_jsRuntime, "TestDb", "TestStore");
        await interop.OpenAsync();
        await interop.DisposeAsync();

        // Act & Assert
        Assert.ThrowsAsync<ObjectDisposedException>(async () => 
            await interop.GetAsync(GetBytes("key")));
    }

    #endregion

    #region Helpers

    private static byte[] GetBytes(string s) => System.Text.Encoding.UTF8.GetBytes(s);

    #endregion
}
