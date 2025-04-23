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
    IGenerateStepwiseBuilderAddStep BranchFrom(string builderName, string stepName);

    IGenerateStepwiseBuilderAddStep AddStep<TArgument>(string stepName, string? fieldName = null,
        Func<TArgument>? defaultValueFactory = null);
}

public interface IGenerateStepwiseBuilderAddStep
{
    IGenerateStepwiseBuilderAddStep AddStep<TArgument>(string stepName, string? fieldName = null,
        Func<TArgument>? defaultValueFactory = null);

    void CreateBuilderFor<TResult>();
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

    public static IGenerateStepwiseBuilderAddStep BranchFrom(string builderName, string stepName)
    {
        return ((IGenerateStepwiseBuilderInitialSteps)new GenerateStepwiseBuilder()).BranchFrom(builderName, stepName);
    }

    IGenerateStepwiseBuilderAddStep IGenerateStepwiseBuilderInitialSteps.BranchFrom(string builderName, string stepName)
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

    public void CreateBuilderFor<TResult>()
    {
    }
}