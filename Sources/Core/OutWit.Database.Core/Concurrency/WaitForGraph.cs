namespace OutWit.Database.Core.Concurrency
{
    /// <summary>
    /// Wait-For Graph for deadlock detection.
    /// Tracks which transactions are waiting for which other transactions.
    /// A cycle in this graph indicates a deadlock.
    /// </summary>
    public sealed class WaitForGraph
    {
        #region Fields

        private readonly Dictionary<long, HashSet<long>> m_edges = new();
        private readonly Lock m_lock = new();

        #endregion

        #region Edge Management

        /// <summary>
        /// Adds an edge indicating that waiter is waiting for holder.
        /// </summary>
        /// <param name="waiterTxId">Transaction that is waiting.</param>
        /// <param name="holderTxId">Transaction that holds the resource.</param>
        public void AddEdge(long waiterTxId, long holderTxId)
        {
            if (waiterTxId == holderTxId)
                return; // Can't wait for yourself

            lock (m_lock)
            {
                if (!m_edges.TryGetValue(waiterTxId, out var holders))
                {
                    holders = [];
                    m_edges[waiterTxId] = holders;
                }
                holders.Add(holderTxId);
            }
        }

        /// <summary>
        /// Removes an edge (waiter no longer waiting for holder).
        /// </summary>
        /// <param name="waiterTxId">Transaction that was waiting.</param>
        /// <param name="holderTxId">Transaction that held the resource.</param>
        public void RemoveEdge(long waiterTxId, long holderTxId)
        {
            lock (m_lock)
            {
                if (m_edges.TryGetValue(waiterTxId, out var holders))
                {
                    holders.Remove(holderTxId);
                    if (holders.Count == 0)
                    {
                        m_edges.Remove(waiterTxId);
                    }
                }
            }
        }

        /// <summary>
        /// Removes all edges involving a transaction (when it completes).
        /// </summary>
        /// <param name="txId">Transaction ID to remove.</param>
        public void RemoveTransaction(long txId)
        {
            lock (m_lock)
            {
                // Remove as waiter
                m_edges.Remove(txId);

                // Remove as holder from all waiters
                foreach (var holders in m_edges.Values)
                {
                    holders.Remove(txId);
                }

                // Clean up empty entries
                var emptyWaiters = m_edges
                    .Where(kv => kv.Value.Count == 0)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var waiter in emptyWaiters)
                {
                    m_edges.Remove(waiter);
                }
            }
        }

        #endregion

        #region Cycle Detection

        /// <summary>
        /// Detects if there's a cycle in the wait-for graph (deadlock).
        /// </summary>
        /// <returns>True if a deadlock exists.</returns>
        public bool HasCycle()
        {
            lock (m_lock)
            {
                return FindCycle() != null;
            }
        }

        /// <summary>
        /// Detects if adding a new edge would create a cycle.
        /// </summary>
        /// <param name="waiterTxId">Potential waiter.</param>
        /// <param name="holderTxId">Potential holder.</param>
        /// <returns>True if adding this edge would create a deadlock.</returns>
        public bool WouldCreateCycle(long waiterTxId, long holderTxId)
        {
            if (waiterTxId == holderTxId)
                return true;

            lock (m_lock)
            {
                // Temporarily add edge
                AddEdgeInternal(waiterTxId, holderTxId);

                var hasCycle = FindCycleInternal() != null;

                // Remove temporary edge
                RemoveEdgeInternal(waiterTxId, holderTxId);

                return hasCycle;
            }
        }

        /// <summary>
        /// Finds a cycle in the graph and returns the transaction IDs involved.
        /// </summary>
        /// <returns>List of transaction IDs forming the cycle, or null if no cycle.</returns>
        public IReadOnlyList<long>? FindCycle()
        {
            lock (m_lock)
            {
                return FindCycleInternal();
            }
        }

        /// <summary>
        /// Finds all cycles in the graph.
        /// </summary>
        /// <returns>List of cycles, where each cycle is a list of transaction IDs.</returns>
        public IReadOnlyList<IReadOnlyList<long>> FindAllCycles()
        {
            lock (m_lock)
            {
                var cycles = new List<IReadOnlyList<long>>();
                var visited = new HashSet<long>();
                var recursionStack = new HashSet<long>();

                foreach (var node in m_edges.Keys)
                {
                    if (!visited.Contains(node))
                    {
                        var path = new List<long>();
                        FindCyclesDfs(node, visited, recursionStack, path, cycles);
                    }
                }

                return cycles;
            }
        }

        #endregion

        #region Internal Methods

        private void AddEdgeInternal(long waiterTxId, long holderTxId)
        {
            if (!m_edges.TryGetValue(waiterTxId, out var holders))
            {
                holders = [];
                m_edges[waiterTxId] = holders;
            }
            holders.Add(holderTxId);
        }

        private void RemoveEdgeInternal(long waiterTxId, long holderTxId)
        {
            if (m_edges.TryGetValue(waiterTxId, out var holders))
            {
                holders.Remove(holderTxId);
                if (holders.Count == 0)
                {
                    m_edges.Remove(waiterTxId);
                }
            }
        }

        private IReadOnlyList<long>? FindCycleInternal()
        {
            var visited = new HashSet<long>();
            var recursionStack = new HashSet<long>();
            var parent = new Dictionary<long, long>();

            foreach (var node in m_edges.Keys)
            {
                if (!visited.Contains(node))
                {
                    var cycle = FindCycleDfs(node, visited, recursionStack, parent);
                    if (cycle != null)
                        return cycle;
                }
            }

            return null;
        }

        private IReadOnlyList<long>? FindCycleDfs(
            long node,
            HashSet<long> visited,
            HashSet<long> recursionStack,
            Dictionary<long, long> parent)
        {
            visited.Add(node);
            recursionStack.Add(node);

            if (m_edges.TryGetValue(node, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    if (!visited.Contains(neighbor))
                    {
                        parent[neighbor] = node;
                        var cycle = FindCycleDfs(neighbor, visited, recursionStack, parent);
                        if (cycle != null)
                            return cycle;
                    }
                    else if (recursionStack.Contains(neighbor))
                    {
                        // Found cycle - reconstruct it
                        var cycleList = new List<long> { neighbor };
                        var current = node;
                        while (current != neighbor)
                        {
                            cycleList.Add(current);
                            current = parent.GetValueOrDefault(current, neighbor);
                        }
                        cycleList.Add(neighbor);
                        cycleList.Reverse();
                        return cycleList;
                    }
                }
            }

            recursionStack.Remove(node);
            return null;
        }

        private void FindCyclesDfs(
            long node,
            HashSet<long> visited,
            HashSet<long> recursionStack,
            List<long> path,
            List<IReadOnlyList<long>> cycles)
        {
            visited.Add(node);
            recursionStack.Add(node);
            path.Add(node);

            if (m_edges.TryGetValue(node, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    if (recursionStack.Contains(neighbor))
                    {
                        // Found cycle
                        var cycleStart = path.IndexOf(neighbor);
                        if (cycleStart >= 0)
                        {
                            var cycle = path.Skip(cycleStart).ToList();
                            cycle.Add(neighbor);
                            cycles.Add(cycle);
                        }
                    }
                    else if (!visited.Contains(neighbor))
                    {
                        FindCyclesDfs(neighbor, visited, recursionStack, path, cycles);
                    }
                }
            }

            path.RemoveAt(path.Count - 1);
            recursionStack.Remove(node);
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Gets all transactions that a given transaction is waiting for.
        /// </summary>
        /// <param name="waiterTxId">The waiting transaction.</param>
        /// <returns>Set of transaction IDs being waited for.</returns>
        public IReadOnlySet<long> GetWaitingFor(long waiterTxId)
        {
            lock (m_lock)
            {
                if (m_edges.TryGetValue(waiterTxId, out var holders))
                {
                    return new HashSet<long>(holders);
                }
                return new HashSet<long>();
            }
        }

        /// <summary>
        /// Gets all transactions waiting for a given transaction.
        /// </summary>
        /// <param name="holderTxId">The holding transaction.</param>
        /// <returns>Set of waiting transaction IDs.</returns>
        public IReadOnlySet<long> GetWaiters(long holderTxId)
        {
            lock (m_lock)
            {
                var waiters = new HashSet<long>();
                foreach (var (waiter, holders) in m_edges)
                {
                    if (holders.Contains(holderTxId))
                    {
                        waiters.Add(waiter);
                    }
                }
                return waiters;
            }
        }

        /// <summary>
        /// Checks if a transaction is waiting for anything.
        /// </summary>
        /// <param name="txId">Transaction ID.</param>
        /// <returns>True if waiting.</returns>
        public bool IsWaiting(long txId)
        {
            lock (m_lock)
            {
                return m_edges.TryGetValue(txId, out var holders) && holders.Count > 0;
            }
        }

        /// <summary>
        /// Gets the total number of edges in the graph.
        /// </summary>
        public int EdgeCount
        {
            get
            {
                lock (m_lock)
                {
                    return m_edges.Values.Sum(h => h.Count);
                }
            }
        }

        /// <summary>
        /// Gets the number of transactions in the graph.
        /// </summary>
        public int NodeCount
        {
            get
            {
                lock (m_lock)
                {
                    var nodes = new HashSet<long>(m_edges.Keys);
                    foreach (var holders in m_edges.Values)
                    {
                        foreach (var h in holders)
                        {
                            nodes.Add(h);
                        }
                    }
                    return nodes.Count;
                }
            }
        }

        #endregion

        #region Clear

        /// <summary>
        /// Clears the entire graph.
        /// </summary>
        public void Clear()
        {
            lock (m_lock)
            {
                m_edges.Clear();
            }
        }

        #endregion
    }
}
