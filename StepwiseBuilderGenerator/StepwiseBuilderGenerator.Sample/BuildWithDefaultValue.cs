using System;

namespace StepwiseBuilderGenerator.Sample;

[StepwiseBuilder]
public partial class BuildWithDefaultValuesSimpleExample
{
    public BuildWithDefaultValuesSimpleExample()
    {
        GenerateStepwiseBuilder
            .AddStep<int>("SetAge")
            .AddStep<string>("SetName", "Name", () => "John")
            .AddStep<string>("SetLastName")
            .CreateBuilderFor<BuildWithDefaultValuesSimpleExample, string>(b => b.Name);
    }
}

[StepwiseBuilder]
public partial class BuildWithDefaultValuesAsOnlyCallExample
{
    public BuildWithDefaultValuesAsOnlyCallExample()
    {
        GenerateStepwiseBuilder
            .AddStep<string>("SetName", "Name", () => "John")
            .CreateBuilderFor<BuildWithDefaultValuesAsOnlyCallExample, string>(b => b.Name);
    }
}

[StepwiseBuilder]
public partial class BuildWithDefaultValuesAsOnlySeveralCallsExample
{
    public BuildWithDefaultValuesAsOnlySeveralCallsExample()
    {
        GenerateStepwiseBuilder
            .AddStep<string>("SetName", "Name", () => "John")
            .AddStep<string>("SetLastName", "LastName", () => "Snow")
            .AddStep<string>("SetTown", "Town", () => "Wall")
            .CreateBuilderFor<BuildWithDefaultValuesAsOnlySeveralCallsExample, string>(b => b.Name);
    }
}

[StepwiseBuilder]
public partial class BuildWithDefaultValuesWithGenericExample<T, T1> 
    where T : Exception
    where T1 : Exception
{
    public BuildWithDefaultValuesWithGenericExample()
    {
        GenerateStepwiseBuilder
            .AddStep<string>("SetName", "Name", () => "John")
            .AddStep<T>("SetLastName", "LastName", () => default)
            .AddStep<string>("SetTown", "Town", () => "Wall")
            .CreateBuilderFor<BuildWithDefaultValuesWithGenericExample<T, T1>, string>(b => b.Name);
    }
}

[StepwiseBuilder]
// Extension method with default value factory isn't generated
public partial class BuildWithDefaultValuesWithWrongBuilderTypeForDefaultValueFactoryExample 
{
    public BuildWithDefaultValuesWithWrongBuilderTypeForDefaultValueFactoryExample()
    {
        GenerateStepwiseBuilder
            .AddStep<string>("SetName", "Name", () => "John")
            .AddStep<string>("SetLastName", "LastName", () => default)
            .AddStep<string>("SetTown", "Town", () => "Wall")
            .CreateBuilderFor<BuildWithDefaultValuesAsOnlySeveralCallsExample, string>(b => b.Name);
    }
}