using System;
using System.Collections.Generic;

namespace StepwiseBuilderGenerator.Sample3
{
    public interface MockInterface
    {
    }

    [StepwiseBuilder]
    public partial class SimpleBuilderSidePathFromFirstStep
    {
        public SimpleBuilderSidePathFromFirstStep()
        {
            new GenerateStepwiseBuilder()
                .BranchFrom("SimpleBuilder", "FirstStep")
                .AddStep<int>("FirstStep")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
    }

    [StepwiseBuilder]
    public partial class SimpleBuilderSidePathFromMiddleStep
    {
        public SimpleBuilderSidePathFromMiddleStep()
        {
            new GenerateStepwiseBuilder()
                .BranchFrom("SimpleBuilder", "SecondStep")
                .AddStep<int>("FirstStep")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
    }

    [StepwiseBuilder]
    public partial class SimpleGenericBuilderSidePath<T, T1>
        where T : Exception, IList<T>
        where T1 : Exception
    {
        public SimpleGenericBuilderSidePath()
        {
            new GenerateStepwiseBuilder()
                .BranchFrom("BuilderWithGenericParameter", "SecondStep")
                .AddStep<int>("FirstStep")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
    }

    [StepwiseBuilder]
    public partial class GenericBuilderSidePathWithGenerics<T, T1>
        where T : Exception, IList<T>
        where T1 : Exception
    {
        public GenericBuilderSidePathWithGenerics()
        {
            new GenerateStepwiseBuilder()
                .BranchFrom("BuilderWithGenericParameter", "SecondStep")
                .AddStep<T>("FirstStep")
                .AddStep<T1>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
    }
}