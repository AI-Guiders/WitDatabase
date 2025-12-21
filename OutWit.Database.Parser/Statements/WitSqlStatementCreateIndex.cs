using OutWit.Common.Abstract;
using OutWit.Common.Attributes;
using OutWit.Common.Collections;
using OutWit.Common.Values;
using OutWit.Database.Parser.Interfaces;
using OutWit.Database.Parser.Schema.Clauses;

namespace OutWit.Database.Parser.Statements
{
    public class WitSqlStatementCreateIndex : WitSqlStatement
    {
        #region Functions

        public override T Accept<T>(IWitSqlVisitor<T> visitor)
        {
            return visitor.VisitStatementCreateIndex(this);
        }

        #endregion

        #region Model Base

        public override bool Is(ModelBase? other, double tolerance = DEFAULT_TOLERANCE)
        {
            if (other is not WitSqlStatementCreateIndex create)
                return false;

            return base.Is(create, tolerance)
                   && IndexName.Is(create.IndexName)
                   && TableName.Is(create.TableName)
                   && IsUnique.Is(create.IsUnique)
                   && IfNotExists.Is(create.IfNotExists)
                   && Columns.Is(create.Columns);
        }

        public override WitSqlStatementCreateIndex Clone()
        {
            return new WitSqlStatementCreateIndex
            {
                Line = Line,
                Column = Column,
                IndexName = IndexName,
                TableName = TableName,
                IsUnique = IsUnique,
                IfNotExists = IfNotExists,
                Columns = Columns.Select(column => column.Clone()).ToList()
            };
        }

        #endregion

        #region Properties

        [ToString]
        public required string IndexName { get; init; }
        [ToString]
        public required string TableName { get; init; }
        public bool IsUnique { get; init; }
        public bool IfNotExists { get; init; }
        public required IReadOnlyList<ClauseIndexColumn> Columns { get; init; }

        #endregion
    }
}