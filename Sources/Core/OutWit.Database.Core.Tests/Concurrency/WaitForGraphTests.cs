using NUnit.Framework;
using OutWit.Database.Core.Concurrency;

namespace OutWit.Database.Core.Tests.Concurrency
{
    /// <summary>
    /// Unit tests for WaitForGraph.
    /// </summary>
    [TestFixture]
    public class WaitForGraphTests
    {
        #region Edge Management

        [Test]
        public void AddEdgeCreatesEdgeTest()
        {
            var graph = new WaitForGraph();

            graph.AddEdge(1, 2);

            Assert.That(graph.EdgeCount, Is.EqualTo(1));
            Assert.That(graph.IsWaiting(1), Is.True);
            Assert.That(graph.GetWaitingFor(1), Contains.Item(2L));
        }

        [Test]
        public void AddEdgeToSelfIsIgnoredTest()
        {
            var graph = new WaitForGraph();

            graph.AddEdge(1, 1);

            Assert.That(graph.EdgeCount, Is.EqualTo(0));
        }

        [Test]
        public void RemoveEdgeRemovesEdgeTest()
        {
            var graph = new WaitForGraph();
            graph.AddEdge(1, 2);

            graph.RemoveEdge(1, 2);

            Assert.That(graph.EdgeCount, Is.EqualTo(0));
            Assert.That(graph.IsWaiting(1), Is.False);
        }

        [Test]
        public void RemoveTransactionRemovesAllEdgesTest()
        {
            var graph = new WaitForGraph();
            graph.AddEdge(1, 2);
            graph.AddEdge(1, 3);
            graph.AddEdge(2, 1);

            graph.RemoveTransaction(1);

            Assert.That(graph.IsWaiting(1), Is.False);
            Assert.That(graph.GetWaiters(1).Count, Is.EqualTo(0));
        }

        #endregion

        #region Simple Cycle Detection

        [Test]
        public void NoCycleDetectedWhenNoneExistsTest()
        {
            var graph = new WaitForGraph();
            graph.AddEdge(1, 2);
            graph.AddEdge(2, 3);

            Assert.That(graph.HasCycle(), Is.False);
            Assert.That(graph.FindCycle(), Is.Null);
        }

        [Test]
        public void SimpleTwoNodeCycleDetectedTest()
        {
            var graph = new WaitForGraph();
            graph.AddEdge(1, 2);
            graph.AddEdge(2, 1);

            Assert.That(graph.HasCycle(), Is.True);
            var cycle = graph.FindCycle();
            Assert.That(cycle, Is.Not.Null);
            Assert.That(cycle!.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void ThreeNodeCycleDetectedTest()
        {
            var graph = new WaitForGraph();
            graph.AddEdge(1, 2);
            graph.AddEdge(2, 3);
            graph.AddEdge(3, 1);

            Assert.That(graph.HasCycle(), Is.True);
            var cycle = graph.FindCycle();
            Assert.That(cycle, Is.Not.Null);
        }

        [Test]
        public void SelfLoopWouldCreateCycleTest()
        {
            var graph = new WaitForGraph();

            Assert.That(graph.WouldCreateCycle(1, 1), Is.True);
        }

        [Test]
        public void WouldCreateCycleReturnsTrueTest()
        {
            var graph = new WaitForGraph();
            graph.AddEdge(1, 2);
            graph.AddEdge(2, 3);

            // Adding 3 -> 1 would create cycle
            Assert.That(graph.WouldCreateCycle(3, 1), Is.True);
        }

        [Test]
        public void WouldCreateCycleReturnsFalseTest()
        {
            var graph = new WaitForGraph();
            graph.AddEdge(1, 2);
            graph.AddEdge(2, 3);

            // Adding 3 -> 4 would not create cycle
            Assert.That(graph.WouldCreateCycle(3, 4), Is.False);
        }

        #endregion

        #region Complex Cycles

        [Test]
        public void LargeCycleDetectedTest()
        {
            var graph = new WaitForGraph();
            // Create cycle: 1 -> 2 -> 3 -> 4 -> 5 -> 1
            graph.AddEdge(1, 2);
            graph.AddEdge(2, 3);
            graph.AddEdge(3, 4);
            graph.AddEdge(4, 5);
            graph.AddEdge(5, 1);

            Assert.That(graph.HasCycle(), Is.True);
            var cycle = graph.FindCycle();
            Assert.That(cycle, Is.Not.Null);
            Assert.That(cycle!.Count, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void FindAllCyclesFindsMultipleCyclesTest()
        {
            var graph = new WaitForGraph();
            // Two separate cycles
            graph.AddEdge(1, 2);
            graph.AddEdge(2, 1);
            graph.AddEdge(3, 4);
            graph.AddEdge(4, 3);

            var cycles = graph.FindAllCycles();
            Assert.That(cycles.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void CycleWithBranchDetectedTest()
        {
            var graph = new WaitForGraph();
            // Main path with branch: 1 -> 2 -> 3 -> 1, with 2 -> 4 branch
            graph.AddEdge(1, 2);
            graph.AddEdge(2, 3);
            graph.AddEdge(3, 1);
            graph.AddEdge(2, 4); // Branch

            Assert.That(graph.HasCycle(), Is.True);
        }

        #endregion

        #region Query Methods

        [Test]
        public void GetWaitingForReturnsCorrectSetTest()
        {
            var graph = new WaitForGraph();
            graph.AddEdge(1, 2);
            graph.AddEdge(1, 3);
            graph.AddEdge(1, 4);

            var waitingFor = graph.GetWaitingFor(1);

            Assert.That(waitingFor.Count, Is.EqualTo(3));
            Assert.That(waitingFor, Contains.Item(2L));
            Assert.That(waitingFor, Contains.Item(3L));
            Assert.That(waitingFor, Contains.Item(4L));
        }

        [Test]
        public void GetWaitersReturnsCorrectSetTest()
        {
            var graph = new WaitForGraph();
            graph.AddEdge(1, 4);
            graph.AddEdge(2, 4);
            graph.AddEdge(3, 4);

            var waiters = graph.GetWaiters(4);

            Assert.That(waiters.Count, Is.EqualTo(3));
            Assert.That(waiters, Contains.Item(1L));
            Assert.That(waiters, Contains.Item(2L));
            Assert.That(waiters, Contains.Item(3L));
        }

        [Test]
        public void NodeCountIsCorrectTest()
        {
            var graph = new WaitForGraph();
            graph.AddEdge(1, 2);
            graph.AddEdge(2, 3);

            Assert.That(graph.NodeCount, Is.EqualTo(3));
        }

        [Test]
        public void EdgeCountIsCorrectTest()
        {
            var graph = new WaitForGraph();
            graph.AddEdge(1, 2);
            graph.AddEdge(1, 3);
            graph.AddEdge(2, 3);

            Assert.That(graph.EdgeCount, Is.EqualTo(3));
        }

        #endregion

        #region Clear

        [Test]
        public void ClearRemovesAllDataTest()
        {
            var graph = new WaitForGraph();
            graph.AddEdge(1, 2);
            graph.AddEdge(2, 3);
            graph.AddEdge(3, 1);

            graph.Clear();

            Assert.That(graph.EdgeCount, Is.EqualTo(0));
            Assert.That(graph.NodeCount, Is.EqualTo(0));
            Assert.That(graph.HasCycle(), Is.False);
        }

        #endregion

        #region Thread Safety

        [Test]
        public void ConcurrentAddEdgesWorksTest()
        {
            var graph = new WaitForGraph();
            const int edgeCount = 1000;

            Parallel.For(0, edgeCount, i =>
            {
                graph.AddEdge(i, i + 1);
            });

            Assert.That(graph.EdgeCount, Is.EqualTo(edgeCount));
        }

        [Test]
        public void ConcurrentAddAndRemoveWorksTest()
        {
            var graph = new WaitForGraph();

            var addTask = Task.Run(() =>
            {
                for (int i = 0; i < 500; i++)
                {
                    graph.AddEdge(i, i + 1);
                }
            });

            var removeTask = Task.Run(() =>
            {
                for (int i = 0; i < 500; i++)
                {
                    graph.RemoveEdge(i, i + 1);
                }
            });

            Task.WaitAll(addTask, removeTask);

            // Should not throw, result depends on timing
            _ = graph.EdgeCount;
            _ = graph.HasCycle();
        }

        #endregion
    }
}
