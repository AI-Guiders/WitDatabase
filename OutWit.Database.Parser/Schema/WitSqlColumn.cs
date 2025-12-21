using OutWit.Common.Abstract;
using OutWit.Common.Attributes;
using OutWit.Common.Values;
using OutWit.Common.Collections;
using OutWit.Database.Parser.Schema.ColumnConstraints;

namespace OutWit.Database.Parser.Schema
{
    public sealed class WitSqlColumn : ModelBase
    {
        #region Model Base

        public override bool Is(ModelBase? other, double tolerance = DEFAULT_TOLERANCE)
        {
            if (other is not WitSqlColumn definition)
                return false;

            return Name.Is(definition.Name)
                   && DataType.Is(definition.DataType, tolerance)
                   && Constraints.Is(definition.Constraints);
        }

        public override WitSqlColumn Clone()
        {
            return new WitSqlColumn
            {
                Name = Name,
                DataType = DataType.Clone(),
                Constraints = Constraints?.Select(constraint => (ColumnConstraint)constraint.Clone()).ToList()
            };
        }

        #endregion


        #region Properties

        [ToString]
        public required string Name { get; init; }
        [ToString]
        public required WitSqlDataType DataType { get; init; }
        public IReadOnlyList<ColumnConstraint>? Constraints { get; init; }

        #endregion
    }
}
