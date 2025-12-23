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
            Assert.That((int)IsolationLevel.ReadUncommitted, Is.EqualTo(0));
        }

        [Test]
        public void ReadCommittedHasCorrectValueTest()
        {
            Assert.That((int)IsolationLevel.ReadCommitted, Is.EqualTo(1));
        }

        [Test]
        public void RepeatableReadHasCorrectValueTest()
        {
            Assert.That((int)IsolationLevel.RepeatableRead, Is.EqualTo(2));
        }

        [Test]
        public void SerializableHasCorrectValueTest()
        {
            Assert.That((int)IsolationLevel.Serializable, Is.EqualTo(3));
        }

        [Test]
        public void SnapshotHasCorrectValueTest()
        {
            Assert.That((int)IsolationLevel.Snapshot, Is.EqualTo(4));
        }

        #endregion

        #region Ordering Tests

        [Test]
        public void IsolationLevelsHaveCorrectOrderingTest()
        {
            // Isolation levels should increase in strictness
            Assert.That(IsolationLevel.ReadUncommitted, Is.LessThan(IsolationLevel.ReadCommitted));
            Assert.That(IsolationLevel.ReadCommitted, Is.LessThan(IsolationLevel.RepeatableRead));
            Assert.That(IsolationLevel.RepeatableRead, Is.LessThan(IsolationLevel.Serializable));
        }

        #endregion

        #region Parse Tests

        [Test]
        public void ParseFromStringSucceedsTest()
        {
            Assert.That(Enum.Parse<IsolationLevel>("ReadCommitted"), Is.EqualTo(IsolationLevel.ReadCommitted));
            Assert.That(Enum.Parse<IsolationLevel>("Serializable"), Is.EqualTo(IsolationLevel.Serializable));
            Assert.That(Enum.Parse<IsolationLevel>("Snapshot"), Is.EqualTo(IsolationLevel.Snapshot));
        }

        [Test]
        public void TryParseFromStringSucceedsTest()
        {
            Assert.That(Enum.TryParse<IsolationLevel>("ReadCommitted", out var level), Is.True);
            Assert.That(level, Is.EqualTo(IsolationLevel.ReadCommitted));
        }

        [Test]
        public void TryParseInvalidStringFailsTest()
        {
            Assert.That(Enum.TryParse<IsolationLevel>("Invalid", out _), Is.False);
        }

        #endregion

        #region All Values Tests

        [Test]
        public void AllDefinedValuesCountTest()
        {
            var values = Enum.GetValues<IsolationLevel>();
            Assert.That(values, Has.Length.EqualTo(5));
        }

        [Test]
        public void IsDefinedReturnsTrueForAllValuesTest()
        {
            Assert.That(Enum.IsDefined(IsolationLevel.ReadUncommitted), Is.True);
            Assert.That(Enum.IsDefined(IsolationLevel.ReadCommitted), Is.True);
            Assert.That(Enum.IsDefined(IsolationLevel.RepeatableRead), Is.True);
            Assert.That(Enum.IsDefined(IsolationLevel.Serializable), Is.True);
            Assert.That(Enum.IsDefined(IsolationLevel.Snapshot), Is.True);
        }

        [Test]
        public void IsDefinedReturnsFalseForInvalidValueTest()
        {
            Assert.That(Enum.IsDefined((IsolationLevel)99), Is.False);
        }

        #endregion
    }
}
