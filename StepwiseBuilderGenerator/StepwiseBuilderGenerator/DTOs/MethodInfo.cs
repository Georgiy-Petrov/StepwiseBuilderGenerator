using StepwiseBuilderGenerator.HelpersForCache;

namespace StepwiseBuilderGenerator.DTOs;

internal record MethodInfo(
    string MethodName,
    EquatableArray<string>? GenericArguments,
    EquatableArray<string>? Arguments)
{
    public string MethodName { get; } = MethodName;
    public EquatableArray<string>? GenericArguments { get; } = GenericArguments;
    public EquatableArray<string>? Arguments { get; } = Arguments;
}