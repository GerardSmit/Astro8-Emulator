namespace Yabal.Ast;

public record CreatePointerExpression(SourceRange Range, Expression Value, int BankValue, LanguageType ElementType) : AddressExpression(Range), IConstantValue, IPointerSource
{
    public override void Initialize(YabalBuilder builder)
    {
        Value.Initialize(builder);
    }

    protected override void BuildExpressionCore(YabalBuilder builder, bool isVoid, LanguageType? suggestedType)
    {
        Value.BuildExpression(builder, isVoid, suggestedType);
    }

    public override int? Bank => BankValue;

    public override void StoreAddressInA(YabalBuilder builder)
    {
        Value.BuildExpression(builder, false, null);
    }

    object? IConstantValue.Value => Pointer is {} pointer
        ? RawAddress.From(ElementType, pointer)
        : null;

    public override bool OverwritesB => Value.OverwritesB;

    public override LanguageType Type => LanguageType.Pointer(ElementType);

    public override CreatePointerExpression CloneExpression()
    {
        return new CreatePointerExpression(Range, Value.CloneExpression(), BankValue, ElementType);
    }

    public override Pointer? Pointer => Value is IConstantValue { Value: int value }
        ? new AbsolutePointer(value, BankValue)
        : null;

    int IPointerSource.Bank => BankValue;
}
