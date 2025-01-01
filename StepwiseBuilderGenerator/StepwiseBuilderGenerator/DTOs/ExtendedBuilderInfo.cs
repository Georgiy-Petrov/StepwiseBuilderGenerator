using StepwiseBuilderGenerator.HelpersForCache;

namespace StepwiseBuilderGenerator.DTOs;

internal record ExtendedBuilderInfo(string DeclaredNamespace, string ClassName, EquatableArray<string>? Usings)
{
    public string DeclaredNamespace { get; } = DeclaredNamespace;
    public string ClassName { get; } = ClassName;
    public EquatableArray<string>? Usings { get; } = Usings;
}