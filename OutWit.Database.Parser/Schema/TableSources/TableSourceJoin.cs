using OutWit.Common.Abstract;
using OutWit.Common.Values;
using OutWit.Database.Parser.Expressions;
using OutWit.Database.Parser.Schema.Types;

namespace OutWit.Database.Parser.Schema.TableSources
{
    public sealed class TableSourceJoin : TableSource
    {
        #region Model Base

        public override bool Is(ModelBase? other, double tolerance = DEFAULT_TOLERANCE)
        {
            if (other is not TableSourceJoin join)
                return false;

            return base.Is(join, tolerance)
                   && Left.Is(join.Left, tolerance)
                   && Right.Is(join.Right, tolerance)
                   && JoinType.Is(join.JoinType)
                   && OnCondition.Is(join.OnCondition, tolerance);
        }

        public override TableSourceJoin Clone()
        {
            return new TableSourceJoin
            {
                Left = (TableSource)Left.Clone(),
                Right = (TableSource)Right.Clone(),
                JoinType = JoinType,
                OnCondition = (WitSqlExpression)OnCondition.Clone(),
                Alias = Alias
            };
        }

        #endregion

        #region Properties

        public required TableSource Left { get; init; }
        public required TableSource Right { get; init; }
        public required JoinType JoinType { get; init; }
        public required WitSqlExpression OnCondition { get; init; }

        #endregion
    }
}
