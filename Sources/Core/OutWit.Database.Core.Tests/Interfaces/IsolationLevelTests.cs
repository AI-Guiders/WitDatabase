using NUnit.Framework;
using OutWit.Database.Core.Interfaces;

namespace OutWit.Database.Core.Tests.Interfaces
{
    /// <summary>
    /// Tests for IsolationLevel enum.
    /// </summary>
    [TestFixture]
    public class IsolationLevelTests
    {
        #region Enum Value Tests

        [Test]
        public void ReadUncommittedHasCorrectValueTest()
        {
            Assert.That((int)WitIsolationLevel.ReadUncommitted, Is.EqualTo(0));
        }

        [Test]
        public void ReadCommittedHasCorrectValueTest()
        {
            Assert.That((int)WitIsolationLevel.ReadCommitted, Is.EqualTo(1));
        }

        [Test]
        public void RepeatableReadHasCorrectValueTest()
        {
            Assert.That((int)WitIsolationLevel.RepeatableRead, Is.EqualTo(2));
        }

        [Test]
        public void SerializableHasCorrectValueTest()
        {
            Assert.That((int)WitIsolationLevel.Serializable, Is.EqualTo(3));
        }

        [Test]
        public void SnapshotHasCorrectValueTest()
        {
            Assert.That((int)WitIsolationLevel.Snapshot, Is.EqualTo(4));
        }

        #endregion

        #region Ordering Tests

        [Test]
        public void IsolationLevelsHaveCorrectOrderingTest()
        {
            // Isolation levels should increase in strictness
            Assert.That(WitIsolationLevel.ReadUncommitted, Is.LessThan(WitIsolationLevel.ReadCommitted));
            Assert.That(WitIsolationLevel.ReadCommitted, Is.LessThan(WitIsolationLevel.RepeatableRead));
            Assert.That(WitIsolationLevel.RepeatableRead, Is.LessThan(WitIsolationLevel.Serializable));
        }

        #endregion

        #region Parse Tests

        [Test]
        public void ParseFromStringSucceedsTest()
        {
            Assert.That(Enum.Parse<WitIsolationLevel>("ReadCommitted"), Is.EqualTo(WitIsolationLevel.ReadCommitted));
            Assert.That(Enum.Parse<WitIsolationLevel>("Serializable"), Is.EqualTo(WitIsolationLevel.Serializable));
            Assert.That(Enum.Parse<WitIsolationLevel>("Snapshot"), Is.EqualTo(WitIsolationLevel.Snapshot));
        }

        [Test]
        public void TryParseFromStringSucceedsTest()
        {
            Assert.That(Enum.TryParse<WitIsolationLevel>("ReadCommitted", out var level), Is.True);
            Assert.That(level, Is.EqualTo(WitIsolationLevel.ReadCommitted));
        }

        [Test]
        public void TryParseInvalidStringFailsTest()
        {
            Assert.That(Enum.TryParse<WitIsolationLevel>("Invalid", out _), Is.False);
        }

        #endregion

        #region All Values Tests

        [Test]
        public void AllDefinedValuesCountTest()
        {
            var values = Enum.GetValues<WitIsolationLevel>();
            Assert.That(values, Has.Length.EqualTo(5));
        }

        [Test]
        public void IsDefinedReturnsTrueForAllValuesTest()
        {
            Assert.That(Enum.IsDefined(WitIsolationLevel.ReadUncommitted), Is.True);
            Assert.That(Enum.IsDefined(WitIsolationLevel.ReadCommitted), Is.True);
            Assert.That(Enum.IsDefined(WitIsolationLevel.RepeatableRead), Is.True);
            Assert.That(Enum.IsDefined(WitIsolationLevel.Serializable), Is.True);
            Assert.That(Enum.IsDefined(WitIsolationLevel.Snapshot), Is.True);
        }

        [Test]
        public void IsDefinedReturnsFalseForInvalidValueTest()
        {
            Assert.That(Enum.IsDefined((WitIsolationLevel)99), Is.False);
        }

        #endregion
    }
}
