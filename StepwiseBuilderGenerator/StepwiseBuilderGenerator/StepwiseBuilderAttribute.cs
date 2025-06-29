using System;

namespace StepwiseBuilderGenerator;

[AttributeUsage(AttributeTargets.Class)]
public class StepwiseBuilder : Attribute
{
    public StepwiseBuilder()
    {
    }
}

public interface IGenerateStepwiseBuilderInitialSteps
{
    IGenerateStepwiseBuilderAddStep BranchFromStepBefore<TBuilder>(string stepName);

    IGenerateStepwiseBuilderAddStep AddStep<TArgument>(string stepName, string? fieldName = null,
        Func<TArgument>? defaultValueFactory = null);
}

public interface IGenerateStepwiseBuilderAddStep
{
    IGenerateStepwiseBuilderAddStep AddStep<TArgument>(string stepName, string? fieldName = null,
        Func<TArgument>? defaultValueFactory = null);

    IGenerateStepwiseBuilderAddStep AndOverload<TIn, TOut>(Func<TIn, TOut> mapper, string? newName = null);

    void CreateBuilderFor<TResult>();
    void CreateBuilderFor<TBuilder, TResult>(Func<TBuilder, TResult> defaultValueFactory);
}

public class GenerateStepwiseBuilder : IGenerateStepwiseBuilderInitialSteps, IGenerateStepwiseBuilderAddStep
{
    private GenerateStepwiseBuilder()
    {
    }

    public static IGenerateStepwiseBuilderAddStep AddStep<TArgument>(string stepName, string? fieldName = null,
        Func<TArgument>? defaultValueFactory = null)
    {
        return ((IGenerateStepwiseBuilderInitialSteps)new GenerateStepwiseBuilder()).AddStep<TArgument>(stepName,
            fieldName, defaultValueFactory);
    }

    public static IGenerateStepwiseBuilderAddStep BranchFromStepBefore<TBuilder>(string stepName)
    {
        return ((IGenerateStepwiseBuilderInitialSteps)new GenerateStepwiseBuilder()).BranchFromStepBefore<TBuilder>(stepName);
    }

    IGenerateStepwiseBuilderAddStep IGenerateStepwiseBuilderInitialSteps.BranchFromStepBefore<TBuilder>( string stepName)
    {
        return this;
    }

    IGenerateStepwiseBuilderAddStep IGenerateStepwiseBuilderInitialSteps.AddStep<TArgument>(string stepName,
        string? fieldName = null, Func<TArgument>? defaultValueFactory = null)
    {
        return this;
    }

    IGenerateStepwiseBuilderAddStep IGenerateStepwiseBuilderAddStep.AddStep<TArgument>(string stepName,
        string? fieldName = null, Func<TArgument>? defaultValueFactory = null)
    {
        return this;
    }

    public IGenerateStepwiseBuilderAddStep AndOverload<TIn, TOut>(Func<TIn, TOut> mapper, string? newName = null)
    {
        return this;
    }

    public void CreateBuilderFor<TResult>()
    {
    }

    public void CreateBuilderFor<TBuilder, TResult>(Func<TBuilder, TResult> defaultValueFactory)
    {
    }
}