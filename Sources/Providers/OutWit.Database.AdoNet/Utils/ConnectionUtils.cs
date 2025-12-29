using OutWit.Database.Core.Interfaces;
using System.Data;

namespace OutWit.Database.AdoNet.Utils
{
    internal static class ConnectionUtils
    {
        public static string IsolationName(this WitIsolationLevel me)
        {
            return me switch
            {
                WitIsolationLevel.ReadUncommitted => "READ UNCOMMITTED",
                WitIsolationLevel.ReadCommitted => "READ COMMITTED",
                WitIsolationLevel.RepeatableRead => "REPEATABLE READ",
                WitIsolationLevel.Serializable => "SERIALIZABLE",
                WitIsolationLevel.Snapshot => "SNAPSHOT",
                _ => "READ COMMITTED"
            };
        }

        public static WitIsolationLevel ToWitIsolationLevel(this WitDbIsolationLevel me)
        {
            return me switch
            {
                WitDbIsolationLevel.ReadUncommitted => WitIsolationLevel.ReadUncommitted,
                WitDbIsolationLevel.ReadCommitted => WitIsolationLevel.ReadCommitted,
                WitDbIsolationLevel.RepeatableRead => WitIsolationLevel.RepeatableRead,
                WitDbIsolationLevel.Serializable => WitIsolationLevel.Serializable,
                WitDbIsolationLevel.Snapshot => WitIsolationLevel.Snapshot,
                _ => WitIsolationLevel.ReadCommitted
            };
        }

        public static WitIsolationLevel ToIsolationLevel(this IsolationLevel me)
        {
            return me switch
            {
                IsolationLevel.Unspecified => WitIsolationLevel.ReadCommitted,
                IsolationLevel.Chaos => WitIsolationLevel.ReadUncommitted,
                IsolationLevel.ReadUncommitted => WitIsolationLevel.ReadUncommitted,
                IsolationLevel.ReadCommitted => WitIsolationLevel.ReadCommitted,
                IsolationLevel.RepeatableRead => WitIsolationLevel.RepeatableRead,
                IsolationLevel.Serializable => WitIsolationLevel.Serializable,
                IsolationLevel.Snapshot => WitIsolationLevel.Snapshot,
                _ => WitIsolationLevel.ReadCommitted
            };
        }
    }
}
