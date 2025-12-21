using OutWit.Common.Abstract;
using OutWit.Common.Attributes;
using OutWit.Common.Collections;
using OutWit.Common.Values;
using OutWit.Database.Parser.Expressions;
using OutWit.Database.Parser.Interfaces;
using OutWit.Database.Parser.Schema.Clauses;

namespace OutWit.Database.Parser.Statements
{
    public class WitSqlStatementDelete : WitSqlStatement
    {
        #region Functions

        public override T Accept<T>(IWitSqlVisitor<T> visitor)
        {
            return visitor.VisitStatementDelete(this);
        }

        #endregion

        #region Model Base

        public override bool Is(ModelBase? other, double tolerance = DEFAULT_TOLERANCE)
        {
            if (other is not WitSqlStatementDelete delete)
                return false;

            return base.Is(delete, tolerance)
                   && TableName.Is(delete.TableName)
                   && WhereClause.Check(delete.WhereClause)
                   && ReturningClause.Is(delete.ReturningClause);
        }

        public override WitSqlStatementDelete Clone()
        {
            return new WitSqlStatementDelete
            {
                Line = Line,
                Column = Column,
                TableName = TableName,
                WhereClause = (WitSqlExpression?)WhereClause?.Clone(),
                ReturningClause = ReturningClause?.Select(x => x.Clone()).ToList()
            };
        }

        #endregion

        #region Properties

        [ToString]
        public required string TableName { get; init; }

        public WitSqlExpression? WhereClause { get; init; }

        /// <summary>
        /// RETURNING clause for retrieving deleted values.
        /// </summary>
        public IReadOnlyList<ClauseSelectItem>? ReturningClause { get; init; }

        #endregion
    }
}