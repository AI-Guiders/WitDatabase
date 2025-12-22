using NUnit.Framework;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Query;

namespace OutWit.Database.Core.Tests.Query
{
    [TestFixture]
    public class QueryContextTests
    {
        #region Constructor Tests

        [Test]
        public void DefaultConstructorInitializesCorrectlyTest()
        {
            // Arrange & Act
            var context = new QueryContext();

            // Assert
            Assert.That(context.AffectedRows, Is.EqualTo(-1));
            Assert.That(context.LastInsertId, Is.EqualTo(-1));
            Assert.That(context.TimeoutMilliseconds, Is.EqualTo(0));
            Assert.That(context.IsCancelled, Is.False);
            Assert.That(context.IsTimedOut, Is.False);
            Assert.That(context.CancellationToken.CanBeCanceled, Is.True);
        }

        [Test]
        public void ConstructorWithCancellationTokenInitializesCorrectlyTest()
        {
            // Arrange
            using var cts = new CancellationTokenSource();

            // Act
            var context = new QueryContext(cts.Token);

            // Assert
            Assert.That(context.TimeoutMilliseconds, Is.EqualTo(0));
            Assert.That(context.CancellationToken.CanBeCanceled, Is.True);
        }

        [Test]
        public void ConstructorWithTimeoutInitializesCorrectlyTest()
        {
            // Arrange & Act
            var context = new QueryContext(5000);

            // Assert
            Assert.That(context.TimeoutMilliseconds, Is.EqualTo(5000));
            Assert.That(context.CancellationToken.CanBeCanceled, Is.True);
        }

        [Test]
        public void ConstructorWithTimeoutAndTokenInitializesCorrectlyTest()
        {
            // Arrange
            using var cts = new CancellationTokenSource();

            // Act
            var context = new QueryContext(5000, cts.Token);

            // Assert
            Assert.That(context.TimeoutMilliseconds, Is.EqualTo(5000));
            Assert.That(context.CancellationToken.CanBeCanceled, Is.True);
        }

        #endregion

        #region AffectedRows Tests

        [Test]
        public void SetAffectedRowsUpdatesValueTest()
        {
            // Arrange
            var context = new QueryContext();

            // Act
            context.SetAffectedRows(42);

            // Assert
            Assert.That(context.AffectedRows, Is.EqualTo(42));
        }

        [Test]
        public void IncrementAffectedRowsFromDefaultTest()
        {
            // Arrange
            var context = new QueryContext();

            // Act
            context.IncrementAffectedRows();

            // Assert
            Assert.That(context.AffectedRows, Is.EqualTo(1));
        }

        [Test]
        public void IncrementAffectedRowsMultipleTimesTest()
        {
            // Arrange
            var context = new QueryContext();
            context.SetAffectedRows(10);

            // Act
            context.IncrementAffectedRows(5);
            context.IncrementAffectedRows(3);

            // Assert
            Assert.That(context.AffectedRows, Is.EqualTo(18));
        }

        [Test]
        public void IncrementAffectedRowsIsThreadSafeTest()
        {
            // Arrange
            var context = new QueryContext();
            context.SetAffectedRows(0);
            const int iterations = 1000;
            const int threads = 10;

            // Act
            var tasks = Enumerable.Range(0, threads)
                .Select(_ => Task.Run(() =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        context.IncrementAffectedRows();
                    }
                }))
                .ToArray();

            Task.WaitAll(tasks);

            // Assert
            Assert.That(context.AffectedRows, Is.EqualTo(iterations * threads));
        }

        #endregion

        #region LastInsertId Tests

        [Test]
        public void SetLastInsertIdUpdatesValueTest()
        {
            // Arrange
            var context = new QueryContext();

            // Act
            context.SetLastInsertId(12345);

            // Assert
            Assert.That(context.LastInsertId, Is.EqualTo(12345));
        }

        [Test]
        public void SetLastInsertIdOverwritesPreviousValueTest()
        {
            // Arrange
            var context = new QueryContext();
            context.SetLastInsertId(100);

            // Act
            context.SetLastInsertId(200);

            // Assert
            Assert.That(context.LastInsertId, Is.EqualTo(200));
        }

        #endregion

        #region Cancellation Tests

        [Test]
        public void CancelSetsIsCancelledFlagTest()
        {
            // Arrange
            var context = new QueryContext();

            // Act
            context.Cancel();

            // Assert
            Assert.That(context.IsCancelled, Is.True);
            Assert.That(context.CancellationToken.IsCancellationRequested, Is.True);
        }

        [Test]
        public void ExternalCancellationPropagatesTest()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var context = new QueryContext(cts.Token);

            // Act
            cts.Cancel();

            // Assert
            Assert.That(context.IsCancelled, Is.True);
            Assert.That(context.CancellationToken.IsCancellationRequested, Is.True);
        }

        [Test]
        public void ThrowIfCancellationRequestedThrowsWhenCancelledTest()
        {
            // Arrange
            var context = new QueryContext();
            context.Cancel();

            // Act & Assert
            Assert.Throws<OperationCanceledException>(() => context.ThrowIfCancellationRequested());
        }

        [Test]
        public void ThrowIfCancellationRequestedDoesNotThrowWhenNotCancelledTest()
        {
            // Arrange
            var context = new QueryContext();

            // Act & Assert
            Assert.DoesNotThrow(() => context.ThrowIfCancellationRequested());
        }

        #endregion

        #region Timeout Tests

        [Test]
        public async Task TimeoutSetsIsTimedOutFlagTest()
        {
            // Arrange
            var context = new QueryContext(100); // 100ms timeout

            // Act
            await Task.Delay(200);

            // Assert
            Assert.That(context.IsTimedOut, Is.True);
            Assert.That(context.IsCancelled, Is.True);
        }

        [Test]
        public void NoTimeoutDoesNotSetFlagTest()
        {
            // Arrange
            var context = new QueryContext(0);

            // Act - nothing, just verify initial state

            // Assert
            Assert.That(context.IsTimedOut, Is.False);
        }

        #endregion

        #region Reset Tests

        [Test]
        public void ResetClearsAffectedRowsAndLastInsertIdTest()
        {
            // Arrange
            var context = new QueryContext();
            context.SetAffectedRows(100);
            context.SetLastInsertId(500);

            // Act
            context.Reset();

            // Assert
            Assert.That(context.AffectedRows, Is.EqualTo(-1));
            Assert.That(context.LastInsertId, Is.EqualTo(-1));
        }

        #endregion

        #region Interface Tests

        [Test]
        public void ImplementsIQueryContextTest()
        {
            // Arrange & Act
            var context = new QueryContext();

            // Assert
            Assert.That(context, Is.InstanceOf<IQueryContext>());
        }

        [Test]
        public void InterfaceReturnsCorrectValuesTest()
        {
            // Arrange
            var context = new QueryContext(1000);
            context.SetAffectedRows(5);
            context.SetLastInsertId(42);

            // Act
            IQueryContext interfaceContext = context;

            // Assert
            Assert.That(interfaceContext.AffectedRows, Is.EqualTo(5));
            Assert.That(interfaceContext.LastInsertId, Is.EqualTo(42));
            Assert.That(interfaceContext.TimeoutMilliseconds, Is.EqualTo(1000));
            Assert.That(interfaceContext.IsCancelled, Is.False);
            Assert.That(interfaceContext.IsTimedOut, Is.False);
        }

        #endregion
    }
}
