namespace OutWit.Database.Parser.Schema.Types
{
    public enum BinaryOperatorType
    {
        // Arithmetic
        Add,
        Subtract,
        Multiply,
        Divide,
        Modulo,
    
        // Comparison
        Equal,
        NotEqual,
        LessThan,
        LessOrEqual,
        GreaterThan,
        GreaterOrEqual,
    
        // Logical
        And,
        Or,
    
        // String
        Concat,
    
        // Bitwise
        BitwiseAnd,
        BitwiseOr,
        LeftShift,
        RightShift
    }
}
