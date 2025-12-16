using OutWit.Database.Core.Comparers;

namespace OutWit.Database.Core.Tests.Comparers;

[TestFixture]
public class ByteArrayComparerTest
{
    #region Compare Tests

    [Test]
    public void CompareEqualArraysTest()
    {
        byte[] a = [1, 2, 3];
        byte[] b = [1, 2, 3];
        
        Assert.That(ByteArrayComparer.Default.Compare(a, b), Is.EqualTo(0));
    }

    [Test]
    public void CompareSameReferenceTest()
    {
        byte[] a = [1, 2, 3];
        
        Assert.That(ByteArrayComparer.Default.Compare(a, a), Is.EqualTo(0));
    }

    [Test]
    public void CompareNullsTest()
    {
        byte[] a = [1, 2, 3];
        
        Assert.That(ByteArrayComparer.Default.Compare(null, null), Is.EqualTo(0));
        Assert.That(ByteArrayComparer.Default.Compare(null, a), Is.LessThan(0));
        Assert.That(ByteArrayComparer.Default.Compare(a, null), Is.GreaterThan(0));
    }

    [Test]
    public void CompareLexicographicOrderTest()
    {
        byte[] smaller = [1, 2, 3];
        byte[] larger = [1, 2, 4];
        
        Assert.That(ByteArrayComparer.Default.Compare(smaller, larger), Is.LessThan(0));
        Assert.That(ByteArrayComparer.Default.Compare(larger, smaller), Is.GreaterThan(0));
    }

    [Test]
    public void CompareDifferentLengthsTest()
    {
        byte[] shorter = [1, 2];
        byte[] longer = [1, 2, 3];
        
        Assert.That(ByteArrayComparer.Default.Compare(shorter, longer), Is.LessThan(0));
        Assert.That(ByteArrayComparer.Default.Compare(longer, shorter), Is.GreaterThan(0));
    }

    [Test]
    public void ComparePrefixTest()
    {
        byte[] prefix = [1, 2];
        byte[] full = [1, 2, 0];
        
        // Prefix is shorter, so it comes first
        Assert.That(ByteArrayComparer.Default.Compare(prefix, full), Is.LessThan(0));
    }

    [Test]
    public void CompareEmptyArraysTest()
    {
        byte[] empty1 = [];
        byte[] empty2 = [];
        byte[] nonEmpty = [1];
        
        Assert.That(ByteArrayComparer.Default.Compare(empty1, empty2), Is.EqualTo(0));
        Assert.That(ByteArrayComparer.Default.Compare(empty1, nonEmpty), Is.LessThan(0));
        Assert.That(ByteArrayComparer.Default.Compare(nonEmpty, empty1), Is.GreaterThan(0));
    }

    #endregion

    #region Equals Tests

    [Test]
    public void EqualsEqualArraysTest()
    {
        byte[] a = [1, 2, 3];
        byte[] b = [1, 2, 3];
        
        Assert.That(ByteArrayComparer.Default.Equals(a, b), Is.True);
    }

    [Test]
    public void EqualsSameReferenceTest()
    {
        byte[] a = [1, 2, 3];
        
        Assert.That(ByteArrayComparer.Default.Equals(a, a), Is.True);
    }

    [Test]
    public void EqualsNullsTest()
    {
        byte[] a = [1, 2, 3];
        
        Assert.That(ByteArrayComparer.Default.Equals(null, null), Is.True);
        Assert.That(ByteArrayComparer.Default.Equals(null, a), Is.False);
        Assert.That(ByteArrayComparer.Default.Equals(a, null), Is.False);
    }

    [Test]
    public void EqualsDifferentArraysTest()
    {
        byte[] a = [1, 2, 3];
        byte[] b = [1, 2, 4];
        
        Assert.That(ByteArrayComparer.Default.Equals(a, b), Is.False);
    }

    [Test]
    public void EqualsDifferentLengthsTest()
    {
        byte[] a = [1, 2];
        byte[] b = [1, 2, 3];
        
        Assert.That(ByteArrayComparer.Default.Equals(a, b), Is.False);
    }

    [Test]
    public void EqualsEmptyArraysTest()
    {
        byte[] empty1 = [];
        byte[] empty2 = [];
        
        Assert.That(ByteArrayComparer.Default.Equals(empty1, empty2), Is.True);
    }

    #endregion

    #region GetHashCode Tests

    [Test]
    public void GetHashCodeEqualArraysSameHashTest()
    {
        byte[] a = [1, 2, 3, 4, 5];
        byte[] b = [1, 2, 3, 4, 5];
        
        Assert.That(ByteArrayComparer.Default.GetHashCode(a), 
            Is.EqualTo(ByteArrayComparer.Default.GetHashCode(b)));
    }

    [Test]
    public void GetHashCodeDifferentArraysDifferentHashTest()
    {
        byte[] a = [1, 2, 3];
        byte[] b = [1, 2, 4];
        
        // Different arrays should (usually) have different hashes
        // Note: This is not guaranteed, but very likely for simple cases
        Assert.That(ByteArrayComparer.Default.GetHashCode(a), 
            Is.Not.EqualTo(ByteArrayComparer.Default.GetHashCode(b)));
    }

    [Test]
    public void GetHashCodeNullThrowsTest()
    {
        Assert.Throws<ArgumentNullException>(() => ByteArrayComparer.Default.GetHashCode(null!));
    }

    [Test]
    public void GetHashCodeEmptyArrayTest()
    {
        byte[] empty = [];
        
        // Should not throw
        int hash = ByteArrayComparer.Default.GetHashCode(empty);
        Assert.That(hash, Is.Not.EqualTo(0)); // FNV-1a has non-zero basis
    }

    [Test]
    public void GetHashCodeConsistentTest()
    {
        byte[] array = [1, 2, 3, 4, 5];
        
        int hash1 = ByteArrayComparer.Default.GetHashCode(array);
        int hash2 = ByteArrayComparer.Default.GetHashCode(array);
        
        Assert.That(hash1, Is.EqualTo(hash2));
    }

    #endregion

    #region Dictionary Usage Tests

    [Test]
    public void UsageInDictionaryTest()
    {
        var dict = new Dictionary<byte[], string>(ByteArrayComparer.Default);
        
        byte[] key1 = [1, 2, 3];
        byte[] key2 = [4, 5, 6];
        byte[] key1Copy = [1, 2, 3];
        
        dict[key1] = "value1";
        dict[key2] = "value2";
        
        Assert.That(dict.ContainsKey(key1), Is.True);
        Assert.That(dict.ContainsKey(key1Copy), Is.True); // Same content, different reference
        Assert.That(dict[key1Copy], Is.EqualTo("value1"));
        Assert.That(dict.Count, Is.EqualTo(2));
    }

    [Test]
    public void UsageInHashSetTest()
    {
        var set = new HashSet<byte[]>(ByteArrayComparer.Default);
        
        byte[] a = [1, 2, 3];
        byte[] b = [1, 2, 3]; // Same content
        byte[] c = [4, 5, 6];
        
        set.Add(a);
        set.Add(b); // Should not add (duplicate)
        set.Add(c);
        
        Assert.That(set.Count, Is.EqualTo(2));
        Assert.That(set.Contains([1, 2, 3]), Is.True);
    }

    #endregion

    #region SortedDictionary Usage Tests

    [Test]
    public void UsageInSortedDictionaryTest()
    {
        var dict = new SortedDictionary<byte[], int>(ByteArrayComparer.Default);
        
        dict[[3, 0, 0]] = 3;
        dict[[1, 0, 0]] = 1;
        dict[[2, 0, 0]] = 2;
        
        var keys = dict.Keys.ToList();
        
        Assert.That(keys[0], Is.EqualTo(new byte[] { 1, 0, 0 }));
        Assert.That(keys[1], Is.EqualTo(new byte[] { 2, 0, 0 }));
        Assert.That(keys[2], Is.EqualTo(new byte[] { 3, 0, 0 }));
    }

    #endregion
}
