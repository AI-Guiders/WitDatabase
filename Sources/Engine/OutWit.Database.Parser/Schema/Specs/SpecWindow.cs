using OutWit.Common.Abstract;
using OutWit.Common.Collections;
using OutWit.Database.Parser.Expressions;
using OutWit.Database.Parser.Schema.Clauses;

namespace OutWit.Database.Parser.Schema.Specs;

public sealed class SpecWindow : ModelBase
{
    #region Model Base

    public override bool Is(ModelBase? other, double tolerance = DEFAULT_TOLERANCE)
    {
        if (other is not SpecWindow spec)
            return false;

        return PartitionBy.Is(spec.PartitionBy)
               && OrderBy.Is(spec.OrderBy);
    }

    public override SpecWindow Clone()
    {
        return new SpecWindow
        {
            PartitionBy = PartitionBy?.Select(expression => (WitSqlExpression)expression.Clone()).ToList(),
            OrderBy = OrderBy?.Select(item => item.Clone()).ToList()
        };
    }

    #endregion


    #region Properties

    public IReadOnlyList<WitSqlExpression>? PartitionBy { get; init; }
    public IReadOnlyList<ClauseOrderByItem>? OrderBy { get; init; }

    #endregion
}
