using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StepwiseBuilderGenerator.DTOs;

public record MethodInfo(string MethodName, IEnumerable<string>? GenericArguments, IEnumerable<ArgumentSyntax>? Arguments)
{
    public string MethodName { get; } = MethodName;
    public IEnumerable<string>? GenericArguments { get; } = GenericArguments;
    public IEnumerable<ArgumentSyntax>? Arguments { get; } = Arguments;
}