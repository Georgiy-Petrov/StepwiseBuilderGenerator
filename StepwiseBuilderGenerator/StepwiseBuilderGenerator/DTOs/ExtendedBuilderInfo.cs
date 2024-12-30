using StepwiseBuilderGenerator.HelpersForCache;

namespace StepwiseBuilderGenerator.DTOs;

internal record ExtendedBuilderInfo(string Namespace, string Name, EquatableArray<string>? Usings)
{
    public string Namespace { get; } = Namespace;
    public string Name { get; } = Name;
    public EquatableArray<string>? Usings { get; } = Usings;
}