﻿namespace Yabal.Ast;

public record EmptyStatement(SourceRange Range) : Statement(Range)
{
    public override void Build(YabalBuilder builder)
    {
    }

    public override Statement CloneStatement() => this;

    public override Statement Optimize() => this;
}
