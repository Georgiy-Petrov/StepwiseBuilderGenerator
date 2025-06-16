using System;
using System.Collections.Generic;
using StepwiseBuilderGenerator.Sample;

namespace StepwiseBuilderGenerator.Sample3
{
    /// <summary>
    /// A mock interface used by some of our generic constraints.
    /// </summary>
    public interface MockInterface
    {
    }

    /// <summary>
    /// A side path branching from the 'FirstStep' of a base builder named 'SimpleBuilder'.
    /// Once 'SimpleBuilder' completes 'FirstStep', the user can jump here instead of proceeding
    /// with its own second step. This path has three new steps of its own.
    /// </summary>
    [StepwiseBuilder]
    public partial class SidePathFromFirstStepBuilder
    {
        public SidePathFromFirstStepBuilder()
        {
            GenerateStepwiseBuilder
                .BranchFromStepBefore<SimpleBuilder>( "FirstStep")
                .AddStep<int>("FirstStep")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
    }

    /// <summary>
    /// A side path branching from 'SimpleBuilder' BEFORE 'SecondStep'. So after 'SimpleBuilder'
    /// completes 'FirstStep', the user can switch to this path (FirstStep -> SecondStep -> ThirdStep).
    /// </summary>
    [StepwiseBuilder]
    public partial class SidePathFromMiddleStepBuilder
    {
        public SidePathFromMiddleStepBuilder()
        {
            GenerateStepwiseBuilder
                .BranchFromStepBefore<SimpleBuilder>("SecondStep")
                .AddStep<int>("FirstStep")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
    }

    /// <summary>
    /// Shows a side path from a generic base builder 'BuilderWithGenericParameter',
    /// branching BEFORE the 'SecondStep'. This new builder has its own steps 
    /// (no generics used here).
    /// </summary>
    [StepwiseBuilder]
    public partial class GenericBaseSidePathBuilder<T1, T2, T34>
        where T1 : Exception, IList<T1>
        where T2 : Sample.MockInterface
    {
        public GenericBaseSidePathBuilder()
        {
            GenerateStepwiseBuilder
                .BranchFromStepBefore<MultiGenericParameterBuilder<T1, T2, T34>>("SecondStep")
                .AddStep<int>("FirstStep")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
    }

    /// <summary>
    /// Demonstrates a side path from the same generic base builder ('BuilderWithGenericParameter'),
    /// but here the side path steps also use generics (T, T1). Ensures the extension 
    /// method and the side path builder share compatible type parameters.
    /// </summary>
    [StepwiseBuilder]
    public partial class GenericSidePathWithGenericSteps<T1, T2, T3>
        where T1 : Exception
        where T2 : Sample.MockInterface
    {
        public GenericSidePathWithGenericSteps()
        {
            GenerateStepwiseBuilder
                .BranchFromStepBefore<MultiGenericParameterBuilder<T1, T2, T3>>("SecondStep")
                // Use T, T1 as the step types
                .AddStep<T1>("FirstStep")
                .AddStep<T2>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
    }

    /// <summary>
    /// 1) BranchFromStepBefore the *last* step in 'SimpleBuilder' (which is 'ThirdStep' if that is indeed its final step).
    ///    In many generators, branching from the last step doesn't make much sense, since there's 
    ///    no subsequent step to override. The generator might ignore it or produce limited utility.
    /// 2) The new path has one step called 'AlternateStep'.
    /// </summary>
    [StepwiseBuilder]
    public partial class BranchFromStepBeforeLastStepBuilder
    {
        public BranchFromStepBeforeLastStepBuilder()
        {
            GenerateStepwiseBuilder
                .BranchFromStepBefore<SimpleBuilder>("ThirdStep")
                .AddStep<int>("AlternateStep")
                .CreateBuilderFor<string>();
        }
    }

    /// <summary>
    /// Demonstrates a side path that reuses the *same step name* as the base builder's pivot step.
    /// E.g., BranchFromStepBefore 'SecondStep' and then define the first step in this path 
    /// also as 'SecondStep'. This might be confusing or cause collisions depending 
    /// on how the generator implements extension methods and step interfaces.
    /// </summary>
    [StepwiseBuilder]
    public partial class SidePathWithDuplicateStepName
    {
        public SidePathWithDuplicateStepName()
        {
            GenerateStepwiseBuilder
                .BranchFromStepBefore<SimpleBuilder>("SecondStep")
                // Reusing the same step name 'SecondStep' right after branching
                .AddStep<int>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
    }
}