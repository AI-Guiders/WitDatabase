using OutWit.Common.Abstract;
using OutWit.Common.Collections;
using OutWit.Common.Values;
using OutWit.Database.Parser.Expressions;
using OutWit.Database.Parser.Interfaces;
using OutWit.Database.Parser.Schema.Clauses;
using OutWit.Database.Parser.Schema.TableSources;

namespace OutWit.Database.Parser.Statements
{
    public class WitSqlStatementSelect : WitSqlStatement
    {
        #region Functions

        public override T Accept<T>(IWitSqlVisitor<T> visitor)
        {
            return visitor.VisitStatementSelect(this);
        }

        #endregion

        #region Model Base

        public override bool Is(ModelBase? other, double tolerance = DEFAULT_TOLERANCE)
        {
            if (other is not WitSqlStatementSelect select)
                return false;

            return base.Is(select, tolerance)
                   && IsDistinct.Is(select.IsDistinct)
                   && SelectList.Is(select.SelectList)
                   && FromClause.Is(select.FromClause)
                   && WhereClause.Check(select.WhereClause)
                   && GroupByClause.Is(select.GroupByClause)
                   && HavingClause.Check(select.HavingClause)
                   && OrderByClause.Is(select.OrderByClause)
                   && LimitCount.Check(select.LimitCount)
                   && LimitOffset.Check(select.LimitOffset);
        }

        public override WitSqlStatementSelect Clone()
        {
            return new WitSqlStatementSelect
            {
                Line = Line,
                Column = Column,
                IsDistinct = IsDistinct,
                SelectList = SelectList.Select(item => item.Clone()).ToList(),
                FromClause = FromClause?.Select(source => (TableSource)source.Clone()).ToList(),
                WhereClause = (WitSqlExpression?)WhereClause?.Clone(),
                GroupByClause = GroupByClause?.Select(expression => (WitSqlExpression)expression.Clone()).ToList(),
                HavingClause = (WitSqlExpression?)HavingClause?.Clone(),
                OrderByClause = OrderByClause?.Select(item => item.Clone()).ToList(),
                LimitCount = (WitSqlExpression?)LimitCount?.Clone(),
                LimitOffset = (WitSqlExpression?)LimitOffset?.Clone()
            };
        }

        #endregion

        #region Properties

        public bool IsDistinct { get; init; }
        public required IReadOnlyList<ClauseSelectItem> SelectList { get; init; }
        public IReadOnlyList<TableSource>? FromClause { get; init; }
        public WitSqlExpression? WhereClause { get; init; }
        public IReadOnlyList<WitSqlExpression>? GroupByClause { get; init; }
        public WitSqlExpression? HavingClause { get; init; }
        public IReadOnlyList<ClauseOrderByItem>? OrderByClause { get; set; }  // set for queryExpression
        public WitSqlExpression? LimitCount { get; set; }  // set for queryExpression
        public WitSqlExpression? LimitOffset { get; set; }  // set for queryExpression

        #endregion
    }
}