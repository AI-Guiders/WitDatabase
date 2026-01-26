using MemoryPack;
using OutWit.Common.Abstract;
using OutWit.Common.Attributes;
using OutWit.Common.Collections;
using OutWit.Common.Values;

namespace OutWit.Database.Definitions
{
    /// <summary>
    /// Defines a foreign key constraint.
    /// </summary>
    [MemoryPackable]
    public sealed partial class DefinitionForeignKey : ModelBase
    {
        #region Model Base

        public override bool Is(ModelBase modelBase, double tolerance = 1E-07)
        {
            if (modelBase is not DefinitionForeignKey other)
                return false;

            return Columns.Is(other.Columns)
                && ForeignTable.Is(other.ForeignTable)
                && ForeignColumns.Is(other.ForeignColumns)
                && OnDelete.Is(other.OnDelete)
                && OnUpdate.Is(other.OnUpdate);
        }

        public override DefinitionForeignKey Clone()
        {
            return new DefinitionForeignKey
            {
                Columns = Columns.ToArray(),
                ForeignTable = ForeignTable,
                ForeignColumns = ForeignColumns?.ToArray(),
                OnDelete = OnDelete,
                OnUpdate = OnUpdate
            };
        }

        #endregion

        #region Properties

        /// <summary>
        /// Local column names involved in this FK.
        /// </summary>
        [MemoryPackOrder(0)]
        public required IReadOnlyList<string> Columns { get; init; }

        /// <summary>
        /// Referenced table name.
        /// </summary>
        [ToString]
        [MemoryPackOrder(1)]
        public required string ForeignTable { get; init; }

        /// <summary>
        /// Referenced column names (if null, defaults to PK of foreign table).
        /// </summary>
        [MemoryPackOrder(2)]
        public IReadOnlyList<string>? ForeignColumns { get; init; }

        /// <summary>
        /// Action on delete of referenced row.
        /// </summary>
        [MemoryPackOrder(3)]
        public ReferenceAction OnDelete { get; init; }

        /// <summary>
        /// Action on update of referenced row.
        /// </summary>
        [MemoryPackOrder(4)]
        public ReferenceAction OnUpdate { get; init; }

        #endregion
    }

    /// <summary>
    /// Reference action for FK constraints.
    /// </summary>
    public enum ReferenceAction
    {
        NoAction,
        Restrict,
        Cascade,
        SetNull,
        SetDefault
    }
}