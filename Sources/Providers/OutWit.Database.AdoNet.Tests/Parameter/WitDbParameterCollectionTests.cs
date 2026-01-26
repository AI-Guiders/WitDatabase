using NUnit.Framework;
using System.Data;

namespace OutWit.Database.AdoNet.Tests.Parameter;

/// <summary>
/// Tests for WitDbParameterCollection.
/// </summary>
[TestFixture]
public class WitDbParameterCollectionTests
{
    #region Fields

    private WitDbParameterCollection m_collection = null!;

    #endregion

    #region Setup/TearDown

    [SetUp]
    public void Setup()
    {
        m_collection = new WitDbParameterCollection();
    }

    #endregion

    #region Add Tests

    [Test]
    public void AddParameterReturnsIndexTest()
    {
        var param = new WitDbParameter("@p1", 1);
        
        int index = m_collection.Add((object)param);

        Assert.That(index, Is.EqualTo(0));
    }

    [Test]
    public void AddTypedParameterReturnsParameterTest()
    {
        var param = new WitDbParameter("@p1", 1);
        
        var result = m_collection.Add(param);

        Assert.That(result, Is.SameAs(param));
        Assert.That(m_collection.Count, Is.EqualTo(1));
    }

    [Test]
    public void AddParameterIncreasesCountTest()
    {
        m_collection.Add(new WitDbParameter("@p1", 1));
        m_collection.Add(new WitDbParameter("@p2", 2));

        Assert.That(m_collection.Count, Is.EqualTo(2));
    }

    [Test]
    public void AddWithValueCreatesAndAddsParameterTest()
    {
        var param = m_collection.AddWithValue("@name", "Test");

        Assert.That(param, Is.Not.Null);
        Assert.That(param.ParameterName, Is.EqualTo("@name"));
        Assert.That(param.Value, Is.EqualTo("Test"));
        Assert.That(m_collection.Count, Is.EqualTo(1));
    }

    [Test]
    public void AddRangeAddsMultipleParametersTest()
    {
        var parameters = new WitDbParameter[]
        {
            new("@p1", 1),
            new("@p2", 2),
            new("@p3", 3)
        };

        m_collection.AddRange(parameters);

        Assert.That(m_collection.Count, Is.EqualTo(3));
    }

    [Test]
    public void AddNonWitDbParameterThrowsTest()
    {
        Assert.Throws<ArgumentException>(() => m_collection.Add("not a parameter"));
    }

    #endregion

    #region Remove Tests

    [Test]
    public void ClearRemovesAllParametersTest()
    {
        m_collection.Add(new WitDbParameter("@p1", 1));
        m_collection.Add(new WitDbParameter("@p2", 2));

        m_collection.Clear();

        Assert.That(m_collection.Count, Is.EqualTo(0));
    }

    [Test]
    public void RemoveRemovesParameterTest()
    {
        var param = new WitDbParameter("@p1", 1);
        m_collection.Add(param);

        m_collection.Remove(param);

        Assert.That(m_collection.Count, Is.EqualTo(0));
    }

    [Test]
    public void RemoveAtByIndexRemovesParameterTest()
    {
        m_collection.Add(new WitDbParameter("@p1", 1));
        m_collection.Add(new WitDbParameter("@p2", 2));

        m_collection.RemoveAt(0);

        Assert.That(m_collection.Count, Is.EqualTo(1));
        Assert.That(m_collection[0].ParameterName, Is.EqualTo("@p2"));
    }

    [Test]
    public void RemoveAtByNameRemovesParameterTest()
    {
        m_collection.Add(new WitDbParameter("@p1", 1));
        m_collection.Add(new WitDbParameter("@p2", 2));

        m_collection.RemoveAt("@p1");

        Assert.That(m_collection.Count, Is.EqualTo(1));
        Assert.That(m_collection[0].ParameterName, Is.EqualTo("@p2"));
    }

    #endregion

    #region Contains Tests

    [Test]
    public void ContainsReturnsTrueForExistingParameterTest()
    {
        var param = new WitDbParameter("@p1", 1);
        m_collection.Add(param);

        Assert.That(m_collection.Contains(param), Is.True);
    }

    [Test]
    public void ContainsReturnsFalseForMissingParameterTest()
    {
        var param = new WitDbParameter("@p1", 1);

        Assert.That(m_collection.Contains(param), Is.False);
    }

    [Test]
    public void ContainsByNameReturnsTrueTest()
    {
        m_collection.Add(new WitDbParameter("@p1", 1));

        Assert.That(m_collection.Contains("@p1"), Is.True);
    }

    [Test]
    public void ContainsByNameWithoutPrefixWorksTest()
    {
        m_collection.Add(new WitDbParameter("@p1", 1));

        Assert.That(m_collection.Contains("p1"), Is.True);
    }

    [Test]
    public void ContainsByNameIsCaseInsensitiveTest()
    {
        m_collection.Add(new WitDbParameter("@Name", "value"));

        Assert.That(m_collection.Contains("@name"), Is.True);
        Assert.That(m_collection.Contains("@NAME"), Is.True);
    }

    #endregion

    #region IndexOf Tests

    [Test]
    public void IndexOfReturnsCorrectIndexTest()
    {
        var param1 = new WitDbParameter("@p1", 1);
        var param2 = new WitDbParameter("@p2", 2);
        m_collection.Add(param1);
        m_collection.Add(param2);

        Assert.That(m_collection.IndexOf(param1), Is.EqualTo(0));
        Assert.That(m_collection.IndexOf(param2), Is.EqualTo(1));
    }

    [Test]
    public void IndexOfReturnsMinusOneForMissingTest()
    {
        var param = new WitDbParameter("@p1", 1);

        Assert.That(m_collection.IndexOf(param), Is.EqualTo(-1));
    }

    [Test]
    public void IndexOfByNameReturnsCorrectIndexTest()
    {
        m_collection.Add(new WitDbParameter("@p1", 1));
        m_collection.Add(new WitDbParameter("@p2", 2));

        Assert.That(m_collection.IndexOf("@p1"), Is.EqualTo(0));
        Assert.That(m_collection.IndexOf("@p2"), Is.EqualTo(1));
    }

    [Test]
    public void IndexOfByNameWithDifferentPrefixesWorksTest()
    {
        m_collection.Add(new WitDbParameter("@param", 1));

        Assert.That(m_collection.IndexOf("@param"), Is.EqualTo(0));
        Assert.That(m_collection.IndexOf(":param"), Is.EqualTo(0));
        Assert.That(m_collection.IndexOf("$param"), Is.EqualTo(0));
        Assert.That(m_collection.IndexOf("param"), Is.EqualTo(0));
    }

    #endregion

    #region Insert Tests

    [Test]
    public void InsertInsertsAtCorrectPositionTest()
    {
        m_collection.Add(new WitDbParameter("@p1", 1));
        m_collection.Add(new WitDbParameter("@p3", 3));

        m_collection.Insert(1, new WitDbParameter("@p2", 2));

        Assert.That(m_collection.Count, Is.EqualTo(3));
        Assert.That(m_collection[1].ParameterName, Is.EqualTo("@p2"));
    }

    #endregion

    #region Indexer Tests

    [Test]
    public void IndexerByIndexReturnsParameterTest()
    {
        m_collection.Add(new WitDbParameter("@p1", 1));

        var param = m_collection[0];

        Assert.That(param.ParameterName, Is.EqualTo("@p1"));
    }

    [Test]
    public void IndexerByNameReturnsParameterTest()
    {
        m_collection.Add(new WitDbParameter("@p1", 1));

        var param = m_collection["@p1"];

        Assert.That(param.Value, Is.EqualTo(1));
    }

    [Test]
    public void IndexerByIndexCanSetParameterTest()
    {
        m_collection.Add(new WitDbParameter("@p1", 1));

        m_collection[0] = new WitDbParameter("@p2", 2);

        Assert.That(m_collection[0].ParameterName, Is.EqualTo("@p2"));
    }

    [Test]
    public void IndexerByNameCanSetParameterTest()
    {
        m_collection.Add(new WitDbParameter("@p1", 1));

        m_collection["@p1"] = new WitDbParameter("@p1", 999);

        Assert.That(m_collection["@p1"].Value, Is.EqualTo(999));
    }

    [Test]
    public void IndexerByNameThrowsForMissingParameterTest()
    {
        Assert.Throws<ArgumentException>(() => _ = m_collection["@missing"]);
    }

    #endregion

    #region CopyTo Tests

    [Test]
    public void CopyToCopiesParametersToArrayTest()
    {
        m_collection.Add(new WitDbParameter("@p1", 1));
        m_collection.Add(new WitDbParameter("@p2", 2));

        var array = new WitDbParameter[2];
        m_collection.CopyTo(array, 0);

        Assert.That(array[0].ParameterName, Is.EqualTo("@p1"));
        Assert.That(array[1].ParameterName, Is.EqualTo("@p2"));
    }

    #endregion

    #region Enumeration Tests

    [Test]
    public void GetEnumeratorAllowsIterationTest()
    {
        m_collection.Add(new WitDbParameter("@p1", 1));
        m_collection.Add(new WitDbParameter("@p2", 2));

        var count = 0;
        foreach (WitDbParameter param in m_collection)
        {
            count++;
        }

        Assert.That(count, Is.EqualTo(2));
    }

    #endregion

    #region SyncRoot Tests

    [Test]
    public void SyncRootIsNotNullTest()
    {
        Assert.That(m_collection.SyncRoot, Is.Not.Null);
    }

    #endregion
}
