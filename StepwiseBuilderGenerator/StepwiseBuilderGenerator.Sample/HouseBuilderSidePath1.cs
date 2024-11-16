using System.Threading.Tasks;
using StepwiseBuilderGenerator.Sample;

namespace StepwiseBuilderGenerator.Sample123;

[StepwiseBuilder]
public partial class HouseBuilderSidePath
{
    public HouseBuilderSidePath()
    {
        new GenerateStepwiseBuilder()
            .SidePathFrom("HouseBuilder", "SetDoors")
            .AddStep<int>("Some")
            .CreateBuilderFor<Task<House>>();
    }
}

[StepwiseBuilder]
public partial class HouseBuilderSidePath1
{
    public HouseBuilderSidePath1()
    {
        new GenerateStepwiseBuilder()
            .SidePathFrom("HouseBuilderSidePath", "Some")
            .AddStep<int>("Some2")
            .CreateBuilderFor<Task<House>>();
    }
}
