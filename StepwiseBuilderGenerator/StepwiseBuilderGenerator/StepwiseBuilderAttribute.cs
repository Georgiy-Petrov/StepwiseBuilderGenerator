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
    IGenerateStepwiseBuilderAddStep AddStep<TArgument>(string stepName, string? fieldName = null);
}

public interface IGenerateStepwiseBuilderAddStep
{
    IGenerateStepwiseBuilderAddStep AddStep<TArgument>(string stepName, string? fieldName = null);

    void CreateBuilderFor<TResult>();
}

public class GenerateStepwiseBuilder : IGenerateStepwiseBuilderInitialSteps, IGenerateStepwiseBuilderAddStep
{
    private GenerateStepwiseBuilder()
    {
    }

    public static IGenerateStepwiseBuilderAddStep AddStep<TArgument>(string stepName, string? fieldName = null)
    {
        return ((IGenerateStepwiseBuilderInitialSteps)new GenerateStepwiseBuilder()).AddStep<TArgument>(stepName, fieldName);
    }
    
    public static IGenerateStepwiseBuilderAddStep BranchFrom(string builderName, string stepName)
    {
        return ((IGenerateStepwiseBuilderInitialSteps) new GenerateStepwiseBuilder()).BranchFrom(builderName, stepName);
    }

    IGenerateStepwiseBuilderAddStep IGenerateStepwiseBuilderInitialSteps.BranchFrom(string builderName, string stepName)
    {
        return this;
    }

    IGenerateStepwiseBuilderAddStep IGenerateStepwiseBuilderInitialSteps.AddStep<TArgument>(string stepName, string? fieldName = null)
    {
        return this;
    }
    
    IGenerateStepwiseBuilderAddStep IGenerateStepwiseBuilderAddStep.AddStep<TArgument>(string stepName, string? fieldName = null)
    {
        return this;
    }

    public void CreateBuilderFor<TResult>()
    {
    }
}