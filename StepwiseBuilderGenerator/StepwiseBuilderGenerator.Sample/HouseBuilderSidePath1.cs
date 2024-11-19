using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StepwiseBuilderGenerator.Sample;

namespace StepwiseBuilderGenerator.Sample123
{

    [StepwiseBuilder]
    public partial class HouseBuilderSidePath<T, T1, T2, T3> 
        where T1 : List<int>
        where T2 : Exception
        where T3 : Exception
    {
        public HouseBuilderSidePath()
        {
            new GenerateStepwiseBuilder()
                .SidePathFrom("HouseBuilder", "SetDoors")
                .AddStep<int>("Some")
                .CreateBuilderFor<Task<House>>();
        }
    }

    namespace MyNamespace
    {
        [StepwiseBuilder]
        public partial class HouseBuilderSidePath1<T, T1, T2, T3> 
            where T1 : List<int> 
            where T2 : Exception
            where T3 : Exception
        {
            public HouseBuilderSidePath1()
            {
                new DateTime();
                new DateTime().Add(new TimeSpan());
                new DateTime();
                
                new GenerateStepwiseBuilder()
                    .SidePathFrom("HouseBuilderSidePath", "Some")
                    .AddStep<int>("Some2")
                    .CreateBuilderFor<Task<House>>();
                
                new DateTime();
                new DateTime();
                new DateTime();
            }
        }
    }
}
