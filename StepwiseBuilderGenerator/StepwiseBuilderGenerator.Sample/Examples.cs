using System.Threading.Tasks;

namespace StepwiseBuilderGenerator.Sample
{
    public class House
    {
        public House(Walls walls, Roof roof, Doors doors, Windows windows)
        {
            _walls = walls;
            _roof = roof;
            _doors = doors;
            _windows = windows;
        }

        public class Walls
        {
        }
        
        public class Roof
        {
        }
        
        public class Doors
        {
        }
        
        public class Windows
        {
        }
        
        private Walls _walls;
        private Roof _roof;
        private Doors _doors;
        private Windows _windows;
    }

    public partial class HouseBuilderSidePath
    {
        public HouseBuilderSidePath()
        {
            new GenerateStepwiseBuilder()
                .SidePathFrom("HouseBuilder", "SetRoof")
                .AddStep<House.Walls>("SetWalls", "Walls")
                .AddStep<House.Roof>("SetRoof")
                .AddStep<Task<int>>("SetWindows")
                .AddStep<int>("SetDoors")
                .CreateBuilderFor<Task<House>>();
        }
    }

    [StepwiseBuilder]
    public partial class HouseBuilder
    {
        public HouseBuilder()
        {
            new GenerateStepwiseBuilder()
                .AddStep<House.Walls>("SetWalls", "Walls")
                .AddStep<House.Roof>("SetRoof")
                .AddStep<Task<int>>("SetWindows")
                .AddStep<int>("SetDoors")
                .CreateBuilderFor<Task<House>>();

            new GenerateSidePathForStepwiseBuilder()
                .SidePathFrom("SetDoors")
                .AddStep<House.Roof>("SetRoof")
                .AddStep<Task<int>>("SetWindows")
                .AddStep<int>("SetDoors")
                .CreateBuilderFor<Task<House>>();
            
            new GenerateSidePathForStepwiseBuilder()
                .SidePathFrom("SetWalls")
                .AddStep<House.Roof>("SetWoodenRoof")
                .AddStep<Task<int>>("SetWindows")
                .AddStep<int>("SetDoors")
                .CreateBuilderFor<Task<House>>();
        }
    }

    public static class HouseBuilderExtensions
    {
         public static IHouseBuilderSetWalls HouseBuilder()
         {
             return new HouseBuilder();
         }
                 public static async Task<House> Build(this IHouseBuilderBuild builder)
         {
             return await builder.Build(async b =>
             {
                 var a = await b.SetWindowsValue;
                 return new House(b.Walls, b.SetRoofValue, new House.Doors(), new House.Windows());
             });
         }
    }

    class Test
    {
        public async Task Some()
        {
            var house = await HouseBuilderExtensions.HouseBuilder()
                 .SetWalls(new House.Walls())
                 .SetRoof(new House.Roof())
                 .SetWindows(Task.FromResult(5))
                 .SetDoors(43)
                 .Build();
        }
    }
}