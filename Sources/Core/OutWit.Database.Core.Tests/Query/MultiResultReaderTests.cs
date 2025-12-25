using NUnit.Framework;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Query;
using TextEncoding = System.Text.Encoding;

namespace OutWit.Database.Core.Tests.Query
{
    /// <summary>
    /// Tests for IMultiResultReader and MultiResultReader.
    /// </summary>
    [TestFixture]
    public class MultiResultReaderTests
    {
        #region Helper Methods

        private static byte[] ToBytes(string s) => TextEncoding.UTF8.GetBytes(s);

        private static (byte[] Key, byte[] Value) MakePair(string key, string value)
            => (ToBytes(key), ToBytes(value));

        private static List<(byte[] Key, byte[] Value)> MakeResultSet(params (string key, string value)[] items)
            => items.Select(x => MakePair(x.key, x.value)).ToList();

        #endregion

        #region Constructor Tests

        [Test]
        public void ConstructorWithSingleResultSetTest()
        {
            var data = MakeResultSet(("key1", "value1"), ("key2", "value2"));
            using var reader = new MultiResultReader(data);

            Assert.That(reader.ResultSetCount, Is.EqualTo(1));
            Assert.That(reader.CurrentResultIndex, Is.EqualTo(-1));
            Assert.That(reader.IsClosed, Is.False);
        }

        [Test]
        public void ConstructorWithMultipleResultSetsTest()
        {
            var resultSets = new[]
            {
                new ResultSet(MakeResultSet(("a", "1"))),
                new ResultSet(MakeResultSet(("b", "2"))),
                new ResultSet(MakeResultSet(("c", "3")))
            };

            using var reader = new MultiResultReader(resultSets);

            Assert.That(reader.ResultSetCount, Is.EqualTo(3));
        }

        [Test]
        public void ConstructorWithNullThrowsTest()
        {
            Assert.Throws<ArgumentNullException>(() => new MultiResultReader((IEnumerable<ResultSet>)null!));
        }

        [Test]
        public void EmptyReaderHasNoResultSetsTest()
        {
            using var reader = MultiResultReader.Empty;

            Assert.That(reader.ResultSetCount, Is.EqualTo(0));
            Assert.That(reader.HasMoreResults, Is.False);
        }

        #endregion

        #region NextResult Tests

        [Test]
        public void NextResultMovesToFirstResultSetTest()
        {
            var data = MakeResultSet(("key", "value"));
            using var reader = new MultiResultReader(data);

            Assert.That(reader.CurrentResultIndex, Is.EqualTo(-1));

            var hasNext = reader.NextResult();

            Assert.That(hasNext, Is.True);
            Assert.That(reader.CurrentResultIndex, Is.EqualTo(0));
        }

        [Test]
        public void NextResultMovesToSubsequentResultSetsTest()
        {
            var resultSets = new[]
            {
                new ResultSet(MakeResultSet(("a", "1"))),
                new ResultSet(MakeResultSet(("b", "2"))),
                new ResultSet(MakeResultSet(("c", "3")))
            };

            using var reader = new MultiResultReader(resultSets);

            Assert.That(reader.NextResult(), Is.True);
            Assert.That(reader.CurrentResultIndex, Is.EqualTo(0));

            Assert.That(reader.NextResult(), Is.True);
            Assert.That(reader.CurrentResultIndex, Is.EqualTo(1));

            Assert.That(reader.NextResult(), Is.True);
            Assert.That(reader.CurrentResultIndex, Is.EqualTo(2));

            Assert.That(reader.NextResult(), Is.False);
            Assert.That(reader.CurrentResultIndex, Is.EqualTo(2)); // Stays at last
        }

        [Test]
        public void NextResultOnEmptyReaderReturnsFalseTest()
        {
            using var reader = MultiResultReader.Empty;

            Assert.That(reader.NextResult(), Is.False);
            Assert.That(reader.CurrentResultIndex, Is.EqualTo(-1));
        }

        [Test]
        public async Task NextResultAsyncWorksCorrectlyTest()
        {
            var resultSets = new[]
            {
                new ResultSet(MakeResultSet(("a", "1"))),
                new ResultSet(MakeResultSet(("b", "2")))
            };

            await using var reader = new MultiResultReader(resultSets);

            Assert.That(await reader.NextResultAsync(), Is.True);
            Assert.That(reader.CurrentResultIndex, Is.EqualTo(0));

            Assert.That(await reader.NextResultAsync(), Is.True);
            Assert.That(reader.CurrentResultIndex, Is.EqualTo(1));

            Assert.That(await reader.NextResultAsync(), Is.False);
        }

        #endregion

        #region CurrentResult Tests

        [Test]
        public void CurrentResultBeforeNextResultReturnsNullTest()
        {
            var data = MakeResultSet(("key", "value"));
            using var reader = new MultiResultReader(data);

            Assert.That(reader.CurrentResult, Is.Null);
        }

        [Test]
        public void CurrentResultReturnsDataTest()
        {
            var data = MakeResultSet(("key1", "value1"), ("key2", "value2"));
            using var reader = new MultiResultReader(data);

            reader.NextResult();

            var current = reader.CurrentResult!.ToList();

            Assert.That(current, Has.Count.EqualTo(2));
            Assert.That(current[0].Key, Is.EqualTo(ToBytes("key1")));
            Assert.That(current[0].Value, Is.EqualTo(ToBytes("value1")));
        }

        [Test]
        public void CurrentResultChangesBetweenResultSetsTest()
        {
            var resultSets = new[]
            {
                new ResultSet(MakeResultSet(("a", "1"))),
                new ResultSet(MakeResultSet(("b", "2"), ("c", "3")))
            };

            using var reader = new MultiResultReader(resultSets);

            reader.NextResult();
            var first = reader.CurrentResult!.ToList();
            Assert.That(first, Has.Count.EqualTo(1));

            reader.NextResult();
            var second = reader.CurrentResult!.ToList();
            Assert.That(second, Has.Count.EqualTo(2));
        }

        #endregion

        #region HasMoreResults Tests

        [Test]
        public void HasMoreResultsBeforeFirstNextResultTest()
        {
            var data = MakeResultSet(("key", "value"));
            using var reader = new MultiResultReader(data);

            Assert.That(reader.HasMoreResults, Is.True);
        }

        [Test]
        public void HasMoreResultsAfterLastResultSetTest()
        {
            var data = MakeResultSet(("key", "value"));
            using var reader = new MultiResultReader(data);

            reader.NextResult();

            Assert.That(reader.HasMoreResults, Is.False);
        }

        [Test]
        public void HasMoreResultsWithMultipleResultSetsTest()
        {
            var resultSets = new[]
            {
                new ResultSet(MakeResultSet(("a", "1"))),
                new ResultSet(MakeResultSet(("b", "2")))
            };

            using var reader = new MultiResultReader(resultSets);

            Assert.That(reader.HasMoreResults, Is.True);

            reader.NextResult();
            Assert.That(reader.HasMoreResults, Is.True);

            reader.NextResult();
            Assert.That(reader.HasMoreResults, Is.False);
        }

        #endregion

        #region RecordsAffected Tests

        [Test]
        public void RecordsAffectedBeforeNextResultReturnsMinusOneTest()
        {
            var data = MakeResultSet(("key", "value"));
            using var reader = new MultiResultReader(data);

            Assert.That(reader.RecordsAffected, Is.EqualTo(-1));
        }

        [Test]
        public void RecordsAffectedDefaultIsMinusOneTest()
        {
            var data = MakeResultSet(("key", "value"));
            using var reader = new MultiResultReader(data);

            reader.NextResult();

            Assert.That(reader.RecordsAffected, Is.EqualTo(-1));
        }

        [Test]
        public void RecordsAffectedReturnsSpecifiedValueTest()
        {
            var resultSets = new[]
            {
                new ResultSet(MakeResultSet(("a", "1")), recordsAffected: 5),
                new ResultSet(MakeResultSet(("b", "2")), recordsAffected: 10)
            };

            using var reader = new MultiResultReader(resultSets);

            reader.NextResult();
            Assert.That(reader.RecordsAffected, Is.EqualTo(5));

            reader.NextResult();
            Assert.That(reader.RecordsAffected, Is.EqualTo(10));
        }

        #endregion

        #region ResultSet.Affected Tests

        [Test]
        public void ResultSetAffectedCreatesEmptyResultSetWithCountTest()
        {
            var resultSet = ResultSet.Affected(42);

            Assert.That(resultSet.Data.Count(), Is.EqualTo(0));
            Assert.That(resultSet.RecordsAffected, Is.EqualTo(42));
        }

        [Test]
        public void ResultSetAffectedInReaderTest()
        {
            var resultSets = new[]
            {
                ResultSet.Affected(3),  // INSERT returned 3 rows
                ResultSet.Affected(5),  // UPDATE affected 5 rows
                ResultSet.Affected(1)   // DELETE removed 1 row
            };

            using var reader = new MultiResultReader(resultSets);

            reader.NextResult();
            Assert.That(reader.RecordsAffected, Is.EqualTo(3));
            Assert.That(reader.CurrentResult!.Count(), Is.EqualTo(0));

            reader.NextResult();
            Assert.That(reader.RecordsAffected, Is.EqualTo(5));

            reader.NextResult();
            Assert.That(reader.RecordsAffected, Is.EqualTo(1));
        }

        #endregion

        #region Dispose Tests

        [Test]
        public void DisposeClosesReaderTest()
        {
            var data = MakeResultSet(("key", "value"));
            var reader = new MultiResultReader(data);

            reader.Dispose();

            Assert.That(reader.IsClosed, Is.True);
        }

        [Test]
        public void DisposeMultipleTimesIsIdempotentTest()
        {
            var data = MakeResultSet(("key", "value"));
            var reader = new MultiResultReader(data);

            reader.Dispose();
            reader.Dispose();
            reader.Dispose();

            Assert.That(reader.IsClosed, Is.True);
        }

        [Test]
        public void NextResultAfterDisposeThrowsTest()
        {
            var data = MakeResultSet(("key", "value"));
            var reader = new MultiResultReader(data);

            reader.Dispose();

            Assert.Throws<ObjectDisposedException>(() => reader.NextResult());
        }

        [Test]
        public void CurrentResultAfterDisposeThrowsTest()
        {
            var data = MakeResultSet(("key", "value"));
            var reader = new MultiResultReader(data);
            reader.NextResult();

            reader.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _ = reader.CurrentResult);
        }

        [Test]
        public async Task DisposeAsyncClosesReaderTest()
        {
            var data = MakeResultSet(("key", "value"));
            var reader = new MultiResultReader(data);

            await reader.DisposeAsync();

            Assert.That(reader.IsClosed, Is.True);
        }

        #endregion

        #region Interface Tests

        [Test]
        public void ImplementsIMultiResultReaderTest()
        {
            var data = MakeResultSet(("key", "value"));
            using var reader = new MultiResultReader(data);

            IMultiResultReader iReader = reader;

            Assert.That(iReader, Is.Not.Null);
            Assert.That(iReader.ResultSetCount, Is.EqualTo(1));
        }

        #endregion

        #region Mixed Result Sets Tests

        [Test]
        public void MixedSelectAndDmlResultSetsTest()
        {
            // Simulates: SELECT; INSERT returning 3; SELECT; UPDATE affecting 5
            var resultSets = new[]
            {
                new ResultSet(MakeResultSet(("a", "1"), ("b", "2"))), // SELECT
                ResultSet.Affected(3),                                 // INSERT
                new ResultSet(MakeResultSet(("c", "3"))),             // SELECT
                ResultSet.Affected(5)                                  // UPDATE
            };

            using var reader = new MultiResultReader(resultSets);

            // First SELECT
            Assert.That(reader.NextResult(), Is.True);
            Assert.That(reader.RecordsAffected, Is.EqualTo(-1));
            Assert.That(reader.CurrentResult!.Count(), Is.EqualTo(2));

            // INSERT
            Assert.That(reader.NextResult(), Is.True);
            Assert.That(reader.RecordsAffected, Is.EqualTo(3));
            Assert.That(reader.CurrentResult!.Count(), Is.EqualTo(0));

            // Second SELECT
            Assert.That(reader.NextResult(), Is.True);
            Assert.That(reader.RecordsAffected, Is.EqualTo(-1));
            Assert.That(reader.CurrentResult!.Count(), Is.EqualTo(1));

            // UPDATE
            Assert.That(reader.NextResult(), Is.True);
            Assert.That(reader.RecordsAffected, Is.EqualTo(5));

            // No more results
            Assert.That(reader.NextResult(), Is.False);
        }

        #endregion
    }
}
