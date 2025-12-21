using OutWit.Common.Abstract;
using OutWit.Common.Attributes;
using OutWit.Common.Values;
using OutWit.Common.Collections;
using OutWit.Database.Parser.Expressions;
using OutWit.Database.Parser.Interfaces;
using OutWit.Database.Parser.Schema.Clauses;

namespace OutWit.Database.Parser.Statements
{
    public class WitSqlStatementUpdate : WitSqlStatement
    {
        #region Functions

        public override T Accept<T>(IWitSqlVisitor<T> visitor)
        {
            return visitor.VisitStatementUpdate(this);
        }

        #endregion

        #region Model Base

        public override bool Is(ModelBase? other, double tolerance = DEFAULT_TOLERANCE)
        {
            if (other is not WitSqlStatementUpdate update)
                return false;

            return base.Is(update, tolerance) 
                   && TableName.Is(update.TableName)
                   && SetClauses.Is(update.SetClauses)
                   && WhereClause.Check(update.WhereClause);
        }

        public override WitSqlStatementUpdate Clone()
        {
            return new WitSqlStatementUpdate
            {
                Line = Line,
                Column = Column,
                TableName = TableName,
                SetClauses = SetClauses.Select(set => set.Clone()).ToList(),
                WhereClause = (WitSqlExpression?)WhereClause?.Clone()
            };
        }

        #endregion

        #region Properties

        [ToString]
        public required string TableName { get; init; }
        public required IReadOnlyList<ClauseSet> SetClauses { get; init; }
        public WitSqlExpression? WhereClause { get; init; }

        #endregion
    }
}