using OutWit.Common.Abstract;
using OutWit.Common.Attributes;
using OutWit.Common.Values;
using OutWit.Database.Parser.Interfaces;
using OutWit.Database.Parser.Schema.Types;

namespace OutWit.Database.Parser.Expressions;

public class WitSqlExpressionLiteral : WitSqlExpression
{
    #region Functions

    public override T Accept<T>(IWitSqlVisitor<T> visitor)
    {
        return visitor.VisitExpressionLiteral(this);
    }

    #endregion

    #region Model Base

    public override bool Is(ModelBase? other, double tolerance = DEFAULT_TOLERANCE)
    {
        if (other is not WitSqlExpressionLiteral literal)
            return false;

        return base.Is(literal, tolerance) 
               && Type.Is(literal.Type) 
               && Equals(Value, literal.Value);
    }

    public override WitSqlExpressionLiteral Clone()
    {
        return new WitSqlExpressionLiteral
        {
            Line = Line,
            Column = Column,
            Type = Type,
            Value = Value
        };
    }

    #endregion

    #region Properties

    [ToString]
    public required LiteralType Type { get; init; }

    public object? Value { get; init; }

    #endregion
}