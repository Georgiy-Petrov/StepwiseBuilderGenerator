using System;

namespace StepwiseBuilderGenerator;

[AttributeUsage(AttributeTargets.Class)]
public class StepwiseBuilder : Attribute
{
    // TODO: bool buildersWithFullyQualifiedNames = false
    public StepwiseBuilder()
    {
    }
}

public class GenerateStepwiseBuilder
{
    public GenerateStepwiseBuilder BranchFrom(string builderName, string stepName)
    {
        return this;
    }

    public GenerateStepwiseBuilder AddStep<TArgument>(string stepName, string? fieldName = null)
    {
        return this;
    }

    public void CreateBuilderFor<TResult>()
    {
    }
}