using System;

namespace StepwiseBuilderGenerator;

[AttributeUsage(AttributeTargets.Class)]
public class StepwiseBuilder : Attribute
{
}

public class GenerateSidePathForStepwiseBuilder()
{
    public GenerateStepwiseBuilder SidePathFrom(string stepName)
    {
        return new GenerateStepwiseBuilder();
    }
}

public class GenerateStepwiseBuilder
{
    public GenerateStepwiseBuilder AddStep<T>(string stepName, string? fieldName = null)
    {
        return this;
    }

    public void CreateBuilderFor<TResult>()
    {
    }
}