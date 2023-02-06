namespace Yabal.Ast;

public record SizeOfExpression(SourceRange Range, Expression Expression) : IntegerExpressionBase(Range)
{
    public override void Initialize(YabalBuilder builder)
    {
        Expression.Initialize(builder);
    }

    public override bool OverwritesB => false;

    public override int Value
    {
        get => ((Expression as IConstantValue)?.Value as IAddress)?.Length ?? Type.Size;
        init => throw new NotSupportedException();
    }

    public override string ToString()
    {
        return $"sizeof({Expression})";
    }

    public override Expression CloneExpression()
    {
        return new SizeOfExpression(Range, Expression.CloneExpression());
    }

    public override Expression Optimize()
    {
        return new IntegerExpression(Range, Value);
    }
}
