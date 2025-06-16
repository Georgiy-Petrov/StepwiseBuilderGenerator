using System;
using System.Collections.Generic;

namespace StepwiseBuilderGenerator.Sample
{
    /// <summary>
    /// A simple builder with three steps. This class meets all requirements:
    /// [StepwiseBuilder] attribute, a parameterless constructor, 
    /// a GenerateStepwiseBuilder() call with AddStep(...) and a final CreateBuilderFor(...).
    /// </summary>
    [StepwiseBuilder]
    public partial class BasicThreeStepBuilder
    {
        public BasicThreeStepBuilder()
        {
            GenerateStepwiseBuilder
                .AddStep<int>("FirstStep")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
    }

    /// <summary>
    /// Demonstrates having [StepwiseBuilder] but NO GenerateStepwiseBuilder() call 
    /// in the parameterless constructor, so it will NOT produce a builder.
    /// </summary>
    [StepwiseBuilder]
    public partial class MissingGenerateBuilderCall
    {
        public MissingGenerateBuilderCall()
        {
        }
    }

    /// <summary>
    /// Demonstrates a builder with steps that specify both step names and field names.
    /// The first step has a custom field name "First".
    /// </summary>
    [StepwiseBuilder]
    public partial class OneCustomFieldNameBuilder
    {
        public OneCustomFieldNameBuilder()
        {
            GenerateStepwiseBuilder
                .AddStep<int>("FirstStep", "First")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
    }

    /// <summary>
    /// Demonstrates multiple steps with custom field names ("First" and "Fourth"). 
    /// This will produce a builder reflecting those custom fields.
    /// </summary>
    [StepwiseBuilder]
    public partial class SeveralCustomFieldNamesBuilder
    {
        public SeveralCustomFieldNamesBuilder()
        {
            GenerateStepwiseBuilder
                .AddStep<int>("FirstStep", "First")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep", "Fourth")
                .CreateBuilderFor<string>();
        }
    }

    /// <summary>
    /// Demonstrates that extra statements in the constructor do NOT prevent generating a builder, 
    /// as long as the GenerateStepwiseBuilder() chain is present.
    /// </summary>
    [StepwiseBuilder]
    public partial class ExtraStatementsBuilder
    {
        public ExtraStatementsBuilder()
        {
            // Extra statements
            new object();
            new object();
            new object();

            // Valid builder chain
            GenerateStepwiseBuilder
                .AddStep<int>("FirstStep", "First")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();

            // Extra statements
            new object();
            new object();
            new object();
        }
    }

    /// <summary>
    /// Demonstrates a builder on a class with generic type parameters, including constraints.
    /// [StepwiseBuilder] + parameterless constructor + valid GenerateStepwiseBuilder() chain.
    /// </summary>
    [StepwiseBuilder]
    public partial class GenericParameterBuilder<T, T1>
        where T : Exception, IList<T>
        where T1 : Exception
    {
        public GenericParameterBuilder()
        {
            GenerateStepwiseBuilder
                .AddStep<T>("FirstStep", "First")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
    }

    /// <summary>
    /// Another generic builder with three type parameters and constraints. 
    /// Demonstrates multiple AddStep(...) calls with different generic types.
    /// </summary>
    [StepwiseBuilder]
    public partial class MultiGenericParameterBuilder<T1, T2, T3>
        where T1 : Exception
        where T2 : MockInterface
    {
        public MultiGenericParameterBuilder()
        {
            GenerateStepwiseBuilder
                .AddStep<T1>("FirstStep", "First")
                .AddStep<T2>("SecondStep")
                .AddStep<T3>("ThirdStep")
                .AddStep<object>("FourthStep")
                .CreateBuilderFor<string>();
        }
    }

    /// <summary>
    /// Demonstrates a parameterless constructor with a valid GenerateStepwiseBuilder() chain, 
    /// but NO [StepwiseBuilder] attribute. Will not produce a builder.
    /// </summary>
    public partial class NoAttributeBuilder
    {
        public NoAttributeBuilder()
        {
            GenerateStepwiseBuilder
                .AddStep<int>("FirstStep")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
    }

    /// <summary>
    /// Demonstrates having [StepwiseBuilder], but the constructor(s) include parameters. 
    /// The generator only looks for a parameterless constructor, so no builder is generated.
    /// </summary>
    [StepwiseBuilder]
    public partial class ConstructorsWithParametersBuilder
    {
        public ConstructorsWithParametersBuilder(int a1)
        {
            GenerateStepwiseBuilder
                .AddStep<int>("FirstStep")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }

        public ConstructorsWithParametersBuilder(int a1, int a2, int a3)
        {
            GenerateStepwiseBuilder
                .AddStep<int>("FirstStep")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
    }

    /// <summary>
    /// Has [StepwiseBuilder] and a parameterless constructor, but never calls .CreateBuilderFor<T>(). 
    /// So it will not produce a builder.
    /// </summary>
    [StepwiseBuilder]
    public partial class NoCreateBuilderForCallBuilder
    {
        public NoCreateBuilderForCallBuilder()
        {
            GenerateStepwiseBuilder
                .AddStep<int>("FirstStep")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep");
        }
    }

    /// <summary>
    /// Has [StepwiseBuilder], has a parameterless constructor, 
    /// but does not call GenerateStepwiseBuilder() at all.
    /// Will not produce a builder.
    /// </summary>
    [StepwiseBuilder]
    public partial class EmptyConstructorNoBuilderCalls
    {
        public EmptyConstructorNoBuilderCalls()
        {
        }
    }

    /// <summary>
    /// Demonstrates having multiple GenerateStepwiseBuilder() calls in the same parameterless 
    /// constructor. The generator typically only recognizes the first valid chain.
    /// </summary>
    [StepwiseBuilder]
    public partial class MultipleGenerateCallsBuilder
    {
        public MultipleGenerateCallsBuilder()
        {
            // First builder call
            GenerateStepwiseBuilder
                .AddStep<int>("FirstStep")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();

            // Second builder call - typically ignored by the generator
            GenerateStepwiseBuilder
                .AddStep<int>("FirstStep")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
    }

    /// <summary>
    /// Demonstrates a class with two constructors:
    ///  - A parameterless constructor calling GenerateStepwiseBuilder() (will produce a builder)
    ///  - A constructor with parameters calling GenerateStepwiseBuilder() (will NOT produce a builder)
    /// </summary>
    [StepwiseBuilder]
    public partial class ParameterizedAndParameterlessBuilder
    {
        // This constructor meets the generator's discovery pattern
        public ParameterizedAndParameterlessBuilder()
        {
            GenerateStepwiseBuilder
                .AddStep<int>("FirstStep", "First")
                .AddStep<string>("SecondStep")
                .CreateBuilderFor<string>();
        }

        // The generator ignores this one because it is not parameterless
        public ParameterizedAndParameterlessBuilder(int x, string y)
        {
            GenerateStepwiseBuilder
                .AddStep<int>("ParamFirstStep", "FirstParam")
                .AddStep<string>("ParamSecondStep")
                .CreateBuilderFor<string>();
        }

        public void Test()
        {
            StepwiseBuilders.ParameterizedAndParameterlessBuilder();
            StepwiseBuilders.ParameterizedAndParameterlessBuilder(1, "123");
        }
    }

    /// <summary>
    /// Mock interface referenced by some of our generic constraints.
    /// </summary>
    public interface MockInterface
    {
    }

    /// <summary>
    /// A straightforward base builder with three steps: FirstStep, SecondStep, and ThirdStep.
    /// Other classes can branch off before any of these steps using BranchFromStepBefore(...).
    /// </summary>
    [StepwiseBuilder]
    public partial class SimpleBuilder
    {
        public SimpleBuilder()
        {
            GenerateStepwiseBuilder
                .AddStep<int>("FirstStep", "FirstStepValue")
                .AddStep<int>("SecondStep", "SecondStepValue")
                .AddStep<object>("ThirdStep", "ThirdStepValue")
                .CreateBuilderFor<string>();
        }
    }
}