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
            .AddStep<House.Walls>("SetWalls", "Walls")
            .AddStep<House.Roof>("SetRoof")
            .AddStep<Task<int>>("SetWindows")
            .AddStep<int>("SetDoors")
            .CreateBuilderFor<Task<House>>();
    }
}
