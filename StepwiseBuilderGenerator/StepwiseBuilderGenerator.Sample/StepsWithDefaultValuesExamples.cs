using System;
using System.Threading.Tasks;

namespace StepwiseBuilderGenerator.Sample;

[StepwiseBuilder]
public partial class StepsWithDefaultValuesSimpleExample
{
    public StepsWithDefaultValuesSimpleExample()
    {
        GenerateStepwiseBuilder
            .AddStep<int>("SetAge")
            .AddStep<string>("SetName", "Name", () => "John")
            .AddStep<string>("SetLastName")
            .CreateBuilderFor<string>();
    }
}

[StepwiseBuilder]
public partial class StepsWithDefaultValuesSeveralCallsExample
{
    public StepsWithDefaultValuesSeveralCallsExample()
    {
        GenerateStepwiseBuilder
            .AddStep<int>("SetAge")
            .AddStep<string>("SetName", "Name", () => "John")
            .AddStep<string>("SetLastName", "LastName", () => "Snow")
            .AddStep<string>("SetTown")
            .CreateBuilderFor<string>();
    }
}

[StepwiseBuilder]
public partial class StepsWithDefaultValuesAsLastCallExample
{
    public StepsWithDefaultValuesAsLastCallExample()
    {
        GenerateStepwiseBuilder
            .AddStep<int>("SetAge")
            .AddStep<string>("SetName", "Name", () => "John")
            .AddStep<string>("SetLastName", "LastName", () => "Snow")
            .AddStep<string>("SetTown")
            .CreateBuilderFor<string>();
    }
}

[StepwiseBuilder]
public partial class StepsWithDefaultValuesAsFirstCallExample
{
    public StepsWithDefaultValuesAsFirstCallExample()
    {
        GenerateStepwiseBuilder
            .AddStep<string>("SetName", "Name", () => "John")
            .AddStep<string>("SetLastName", "LastName", () => "Snow")
            .AddStep<string>("SetTown")
            .CreateBuilderFor<string>();
    }
}

[StepwiseBuilder]
public partial class StepsWithDefaultValuesAsOnlyCallExample
{
    public StepsWithDefaultValuesAsOnlyCallExample()
    {
        GenerateStepwiseBuilder
            .AddStep<string>("SetName", "Name", () => "John")
            .CreateBuilderFor<string>();
    }

    public void Test()
    {
        StepwiseBuilders.StepsWithDefaultValuesAsOnlyCallExample().Build(default);
        StepwiseBuilders.StepsWithDefaultValuesAsOnlyCallExample().SetName("123").Build(default);
    }
}

[StepwiseBuilder]
public partial class StepsWithDefaultValuesAsOnlySeveralCallsExample
{
    public StepsWithDefaultValuesAsOnlySeveralCallsExample()
    {
        GenerateStepwiseBuilder
            .AddStep<string>("SetName", "Name", () => "John")
            .AddStep<string>("SetLastName", "LastName", () => "Snow")
            .AddStep<string>("SetTown", "Town", () => "Wall")
            .CreateBuilderFor<string>();
    }
}

[StepwiseBuilder]
public partial class StepsWithDefaultValuesWithArgumentNameCallExample
{
    public StepsWithDefaultValuesWithArgumentNameCallExample()
    {
        GenerateStepwiseBuilder
            .AddStep<string>("SetName", defaultValueFactory: () => "John")
            .AddStep<string>("SetLastName", "LastName", () => "Snow")
            .AddStep<string>("SetTown", "Town", () => "Wall")
            .CreateBuilderFor<string>();
    }
}

//[StepwiseBuilder] -> leads to error because of closure
public partial class StepsWithDefaultValuesWithClosureCallExample
{
    public StepsWithDefaultValuesWithClosureCallExample()
    {
        var someString = "John";

        GenerateStepwiseBuilder
            .AddStep<string>("SetName", defaultValueFactory: () => $"John {someString}")
            .AddStep<string>("SetLastName", "LastName", () => "Snow")
            .AddStep<string>("SetTown", "Town", () => "Wall")
            .CreateBuilderFor<string>();
    }
}

[StepwiseBuilder]
public partial class StepsWithDefaultValuesNavigationBetweenStepsWithDefaultValuesExample
{
    public StepsWithDefaultValuesNavigationBetweenStepsWithDefaultValuesExample()
    {
        GenerateStepwiseBuilder
            .AddStep<string>("SetName", defaultValueFactory: () => "Snow")
            .AddStep<string>("SetLastName", "LastName", () => "Snow")
            .AddStep<string>("SetTown", "Town", () => "Wall")
            .AddStep<string>("SetTown2", "Town2", () => "Wall")
            .AddStep<string>("SetTown3", "Town3", () => "Wall")
            .CreateBuilderFor<StepsWithDefaultValuesNavigationBetweenStepsWithDefaultValuesExample, string>(b => "s");
    }

    public void Test()
    {
        StepwiseBuilders.StepsWithDefaultValuesNavigationBetweenStepsWithDefaultValuesExample().Build();
    }
}

[StepwiseBuilder]
public partial class StepsWithDefaultValuesSeveralCallsWithGapsExample
{
    public StepsWithDefaultValuesSeveralCallsWithGapsExample()
    {
        GenerateStepwiseBuilder
            .AddStep<string>("SetName", "Name", () => "John")
            .AddStep<string>("SetLastName", "LastName", () => "Snow")
            .AddStep<string>("SetLastName2", "LastName2", () => "Snow")
            .AddStep<string>("SetLastName3", "LastName3", () => "Snow")
            .AddStep<string>("SetTown", "Town")
            .AddStep<string>("SetTown2", "Town2", () => "Wall")
            .AddStep<string>("SetTown3", "Town3", () => "Wall")
            .AddStep<string>("SetTown4", "Town4", () => "Wall")
            .CreateBuilderFor<string>();
    }
}

[StepwiseBuilder]
public partial class StepsWithDefaultValuesWithGenericExample<T, T1>
    where T : Exception
    where T1 : Exception
{
    public StepsWithDefaultValuesWithGenericExample()
    {
        GenerateStepwiseBuilder
            .AddStep<string>("SetName", "Name", () => "John")
            .AddStep<T>("SetLastName", "LastName", () => default)
            .AddStep<string>("SetTown", "Town", () => "Wall")
            .CreateBuilderFor<string>();
    }
}

[StepwiseBuilder]
public partial class StepsWithDefaultValuesWithAsyncCallExample
{
    public StepsWithDefaultValuesWithAsyncCallExample()
    {
        GenerateStepwiseBuilder
            .AddStep<string>("SetName", defaultValueFactory: () => "John")
            .AddStep<Task<string>>("SetLastName", "LastName", async () => await Task.FromResult("Snow"))
            .AddStep<string>("SetTown", "Town", () => "Wall")
            .CreateBuilderFor<string>();
    }
}

[StepwiseBuilder]
public partial class StepsWithDefaultValuesSkipToNamedOverloadExample
{
    public StepsWithDefaultValuesSkipToNamedOverloadExample()
    {
        GenerateStepwiseBuilder
            .AddStep<int>("SetAge")
            .AddStep<string>("SetName", "Name", () => "John")
            .AddStep<string>("SetLastName", defaultValueFactory: () => "John123")
            .AddStep<int>("SetYear", defaultValueFactory: () => 42)
            .AndOverload<string, int>(i => int.Parse(i), "SetStringYear")
            .CreateBuilderFor<string>();
    }

    public void Test()
    {
        StepwiseBuilders.StepsWithDefaultValuesSkipToNamedOverloadExample().SetAge(123).SetStringYear("123");
        StepwiseBuilders.StepsWithDefaultValuesSkipToNamedOverloadExample().SetAge(123).SetStringYear("123")
            .Build(default);
        StepwiseBuilders.StepsWithDefaultValuesSkipToNamedOverloadExample().SetStringYear("123");
        StepwiseBuilders.StepsWithDefaultValuesSkipToNamedOverloadExample().SetStringYear("123").SetAge(123);
    }
}

[StepwiseBuilder]
// Currently not possible to fall into branch steps from original builder, if branch builder has no steps without default value
public partial class StepsWithDefaultValuesSkipToNamedOverloadInBranchExample
{
    public StepsWithDefaultValuesSkipToNamedOverloadInBranchExample()
    {
        GenerateStepwiseBuilder
            .BranchFromStepBefore<StepsWithDefaultValuesSkipToNamedOverloadExample>("SetAge")
            .AddStep<int>("SetAge1", defaultValueFactory: () => 42)
            .AddStep<int>("SetAge2", defaultValueFactory: () => 42)
            .AddStep<int>("SetAge3", defaultValueFactory: () => 42)
            .AddStep<int>("SetAge4", defaultValueFactory: () => 42)
            .AndOverload<string, int>(i => int.Parse(i), "SetAge4")
            .AndOverload<object, int>(i => (int)i, "SetAge4")
            .CreateBuilderFor<StepsWithDefaultValuesSkipToNamedOverloadInBranchExample, int>(b => 42);
    }
}

[StepwiseBuilder]
public partial class StepsWithDefaultValuesInBranchFirstStepExample
{
    public StepsWithDefaultValuesInBranchFirstStepExample()
    {
        GenerateStepwiseBuilder
            .BranchFromStepBefore<StepsWithDefaultValuesSkipToNamedOverloadExample>("SetAge")
            .AddStep<int>("SetAgeWithDefault", defaultValueFactory: () => 42)
            .AddStep<int>("SetAgeWithDefault1")
            .CreateBuilderFor<string>();
    }

    public void Test()
    {
        StepwiseBuilders.StepsWithDefaultValuesSkipToNamedOverloadExample().SetAgeWithDefault1(123)
            .SetAgeWithDefault(123);
    }
}