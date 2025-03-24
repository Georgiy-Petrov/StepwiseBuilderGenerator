namespace StepwiseBuilderGenerator.Sample;

[StepwiseBuilder]
public partial class BuilderWithoutDeclaredSystemUsing
{
    public BuilderWithoutDeclaredSystemUsing()
    {
        new GenerateStepwiseBuilder()
            .AddStep<string>("SomeStep")
            .CreateBuilderFor<MockClass>();
    }
}

public class MockClass;