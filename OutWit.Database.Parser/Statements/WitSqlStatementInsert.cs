using OutWit.Common.Abstract;
using OutWit.Common.Attributes;
using OutWit.Common.Collections;
using OutWit.Common.Values;
using OutWit.Database.Parser.Expressions;
using OutWit.Database.Parser.Interfaces;

namespace OutWit.Database.Parser.Statements
{
    public class WitSqlStatementInsert : WitSqlStatement
    {
        #region Functions

        public override T Accept<T>(IWitSqlVisitor<T> visitor)
        {
            return visitor.VisitStatementInsert(this);
        }

        #endregion

        #region Model Base

        public override bool Is(ModelBase? other, double tolerance = DEFAULT_TOLERANCE)
        {
            if (other is not WitSqlStatementInsert insert)
                return false;

            return base.Is(insert, tolerance)
                   && TableName.Is(insert.TableName)
                   && ColumnNames.Is(insert.ColumnNames)
                   && SelectSource.Check(insert.SelectSource)
                   && Values?
                       .SelectMany(expressions => expressions)
                       .ToList()
                       .Is(insert.Values?
                           .SelectMany(expressions => expressions)
                           .ToList()) == true;

        }

        public override WitSqlStatementInsert Clone()
        {
            return new WitSqlStatementInsert
            {
                Line = Line,
                Column = Column,
                TableName = TableName,
                ColumnNames = ColumnNames?.ToList(),
                Values = Values?.Select(row => (IReadOnlyList<WitSqlExpression>)row.Select(x => (WitSqlExpression)x.Clone()).ToList()).ToList(),
                SelectSource = SelectSource?.Clone()
            };
        }

        #endregion

        #region Properties

        [ToString]
        public required string TableName { get; init; }
        public IReadOnlyList<string>? ColumnNames { get; init; }
        public IReadOnlyList<IReadOnlyList<WitSqlExpression>>? Values { get; init; }
        public WitSqlStatementSelect? SelectSource { get; init; }

        #endregion
    }
}