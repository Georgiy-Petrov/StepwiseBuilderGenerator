using System;
using System.Collections.Generic;
using StepwiseBuilderGenerator.HelpersForCache;

namespace StepwiseBuilderGenerator.DTOs;

internal record MethodInfo(
    string MethodName,
    EquatableArray<string>? GenericArguments,
    Dictionary<ArgumentType, string?> Arguments)
{
    public string MethodName { get; } = MethodName;
    public EquatableArray<string>? GenericArguments { get; } = GenericArguments;
    public Dictionary<ArgumentType, string?> Arguments { get; } = Arguments;
}

internal enum ArgumentType
{
    StepName,
    FieldName,
    DefaultValueFactory,
    BuilderName,
    BranchFromStepName,
    AndOverloadMapper,
}

internal static class ArgumentTypeExtensions
{
    public static int ToArgumentOrder(this ArgumentType argumentType)
        =>
            argumentType switch
            {
                ArgumentType.StepName => 0,
                ArgumentType.FieldName => 1,
                ArgumentType.DefaultValueFactory => 2,
                ArgumentType.BuilderName => 0,
                ArgumentType.BranchFromStepName => 1,
                ArgumentType.AndOverloadMapper => 0,
                _ => throw new ArgumentOutOfRangeException(nameof(argumentType), argumentType, null)
            };

    public static string ToArgumentName(this ArgumentType argumentType)
        =>
            argumentType switch
            {
                ArgumentType.StepName => "stepName",
                ArgumentType.FieldName => "fieldName",
                ArgumentType.DefaultValueFactory => "defaultValueFactory",
                ArgumentType.BuilderName => "builderName",
                ArgumentType.BranchFromStepName => "stepName",
                ArgumentType.AndOverloadMapper => "mapper",
                _ => throw new ArgumentOutOfRangeException(nameof(argumentType), argumentType, null)
            };
}