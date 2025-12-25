using NUnit.Framework;
using OutWit.Database.Core.IndexedDb.Indexes;
using OutWit.Database.Core.IndexedDb.Tests.Mocks;
using OutWit.Database.Core.Interfaces;
using System.Text;

namespace OutWit.Database.Core.IndexedDb.Tests.Indexes;

[TestFixture]
public class SecondaryIndexFactoryIndexedDbTests
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
    public void ConstructorWithValidParametersCreatesFactoryTest()
    {
        // Act
        var factory = new SecondaryIndexFactoryIndexedDb(m_jsRuntime, "TestDb");

        // Assert
        Assert.That(factory, Is.Not.Null);
        Assert.That(factory.ProviderKey, Is.EqualTo(SecondaryIndexFactoryIndexedDb.PROVIDER_KEY));
    }

    [Test]
    public void ConstructorWithNullJsRuntimeThrowsTest()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new SecondaryIndexFactoryIndexedDb(null!, "TestDb"));
    }

    [Test]
    public void ConstructorWithNullDatabaseNameThrowsTest()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            new SecondaryIndexFactoryIndexedDb(m_jsRuntime, null!));
    }

    [Test]
    public void ConstructorWithEmptyDatabaseNameThrowsTest()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            new SecondaryIndexFactoryIndexedDb(m_jsRuntime, ""));
    }

    [Test]
    public void ConstructorWithCustomProviderKeyUsesItTest()
    {
        // Act
        var factory = new SecondaryIndexFactoryIndexedDb(m_jsRuntime, "TestDb", "custom_key");

        // Assert
        Assert.That(factory.ProviderKey, Is.EqualTo("custom_key"));
    }

    #endregion

    #region CreateIndex Tests

    [Test]
    public void CreateIndexReturnsSecondaryIndexIndexedDbTest()
    {
        // Arrange
        var factory = new SecondaryIndexFactoryIndexedDb(m_jsRuntime, "TestDb");

        // Act
        using var index = factory.CreateIndex("test_index", isUnique: false);

        // Assert
        Assert.That(index, Is.Not.Null);
        Assert.That(index, Is.InstanceOf<SecondaryIndexIndexedDb>());
        Assert.That(index.Name, Is.EqualTo("test_index"));
        Assert.That(index.IsUnique, Is.False);
    }

    [Test]
    public void CreateUniqueIndexReturnsUniqueIndexTest()
    {
        // Arrange
        var factory = new SecondaryIndexFactoryIndexedDb(m_jsRuntime, "TestDb");

        // Act
        using var index = factory.CreateIndex("unique_index", isUnique: true);

        // Assert
        Assert.That(index.IsUnique, Is.True);
    }

    [Test]
    public void CreateIndexWithNullNameThrowsTest()
    {
        // Arrange
        var factory = new SecondaryIndexFactoryIndexedDb(m_jsRuntime, "TestDb");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => factory.CreateIndex(null!, isUnique: false));
    }

    [Test]
    public void CreateIndexWithEmptyNameThrowsTest()
    {
        // Arrange
        var factory = new SecondaryIndexFactoryIndexedDb(m_jsRuntime, "TestDb");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => factory.CreateIndex("", isUnique: false));
    }

    [Test]
    public void CreateMultipleIndexesCreatesIndependentIndexesTest()
    {
        // Arrange
        var factory = new SecondaryIndexFactoryIndexedDb(m_jsRuntime, "TestDb");

        // Act
        using var index1 = factory.CreateIndex("index1", isUnique: false);
        using var index2 = factory.CreateIndex("index2", isUnique: false);

        index1.Add(GetBytes("key1"), GetBytes("pk1"));
        index2.Add(GetBytes("key2"), GetBytes("pk2"));

        // Assert
        Assert.That(index1.Find(GetBytes("key1")).Any(), Is.True);
        Assert.That(index1.Find(GetBytes("key2")).Any(), Is.False);
        Assert.That(index2.Find(GetBytes("key2")).Any(), Is.True);
        Assert.That(index2.Find(GetBytes("key1")).Any(), Is.False);
    }

    #endregion

    #region ISecondaryIndexFactory Interface Tests

    [Test]
    public void FactoryImplementsISecondaryIndexFactoryTest()
    {
        // Act
        var factory = new SecondaryIndexFactoryIndexedDb(m_jsRuntime, "TestDb");

        // Assert
        Assert.That(factory, Is.InstanceOf<ISecondaryIndexFactory>());
    }

    [Test]
    public void ProviderKeyReturnsIndexedDbTest()
    {
        // Arrange
        var factory = new SecondaryIndexFactoryIndexedDb(m_jsRuntime, "TestDb");

        // Assert
        Assert.That(factory.ProviderKey, Is.EqualTo("indexeddb"));
    }

    #endregion

    #region Helpers

    private static byte[] GetBytes(string s) => System.Text.Encoding.UTF8.GetBytes(s);

    #endregion
}
