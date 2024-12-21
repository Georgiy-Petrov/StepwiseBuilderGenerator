using System;

namespace StepwiseBuilderGenerator;

[AttributeUsage(AttributeTargets.Class)]
public class StepwiseBuilder : Attribute
{
    public StepwiseBuilder(bool buildersWithFullyQualifiedNames = false)
    {
    }
}

public class GenerateStepwiseBuilder
{
    public GenerateStepwiseBuilder SidePathFrom(string builderName, string stepName)
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
