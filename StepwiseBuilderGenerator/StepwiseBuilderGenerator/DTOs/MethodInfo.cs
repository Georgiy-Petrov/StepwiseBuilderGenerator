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
    BranchFromStepBeforeStepName,
    AndOverloadMapper,
    AndOverloadNewName,
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
                ArgumentType.BranchFromStepBeforeStepName => 0,
                ArgumentType.AndOverloadMapper => 0,
                ArgumentType.AndOverloadNewName => 1,
                _ => throw new ArgumentOutOfRangeException(nameof(argumentType), argumentType, null)
            };

    public static string ToArgumentName(this ArgumentType argumentType)
        =>
            argumentType switch
            {
                ArgumentType.StepName => "stepName",
                ArgumentType.FieldName => "fieldName",
                ArgumentType.DefaultValueFactory => "defaultValueFactory",
                ArgumentType.BranchFromStepBeforeStepName => "stepName",
                ArgumentType.AndOverloadMapper => "mapper",
                ArgumentType.AndOverloadNewName => "newName",
                _ => throw new ArgumentOutOfRangeException(nameof(argumentType), argumentType, null)
            };
}