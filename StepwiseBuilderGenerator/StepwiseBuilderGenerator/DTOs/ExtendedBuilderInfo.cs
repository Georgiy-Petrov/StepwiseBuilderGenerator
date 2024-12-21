using StepwiseBuilderGenerator.HelpersForCache;

namespace StepwiseBuilderGenerator.DTOs;

internal record ExtendedBuilderInfo(string Namespace, string Name, (string, string)? Generics, EquatableArray<string>? Usings)
{
    public string Namespace { get; } = Namespace;
    public string Name { get; } = Name;
    public (string, string)? Generics { get; } = Generics;
    public EquatableArray<string>? Usings { get; } = Usings;
}