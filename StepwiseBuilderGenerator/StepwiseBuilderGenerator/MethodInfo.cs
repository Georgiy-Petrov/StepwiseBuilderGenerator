using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StepwiseBuilderGenerator;

public record MethodInfo(string MethodName, IEnumerable<string>? TypeArguments, IEnumerable<ArgumentSyntax>? ArgumentList)
{
    public string MethodName { get; } = MethodName;
    public IEnumerable<string>? TypeArguments { get; } = TypeArguments;
    public IEnumerable<ArgumentSyntax>? ArgumentList { get; } = ArgumentList;
}