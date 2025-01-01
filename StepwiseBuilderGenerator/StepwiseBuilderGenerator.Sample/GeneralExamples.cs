using System;
using System.Collections.Generic;

namespace StepwiseBuilderGenerator.Sample
{
    public interface MockInterface
    {
    }
    
    [StepwiseBuilder]
    public partial class SimpleBuilder
    {
        public SimpleBuilder()
        {
            new GenerateStepwiseBuilder()
                .AddStep<int>("FirstStep")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
    }
    
    //Will not produce builder
    [StepwiseBuilder]
    public partial class SimpleBuilderWithoutNewBuilderDeclaration
    {
        public SimpleBuilderWithoutNewBuilderDeclaration()
        {

        }
    }

    
    [StepwiseBuilder]
    public partial class SimpleBuilderWithOneStepNameFieldName
    {
        public SimpleBuilderWithOneStepNameFieldName()
        {
            new GenerateStepwiseBuilder()
                .AddStep<int>("FirstStep", "First")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
    }
    
    [StepwiseBuilder]
    public partial class SimpleBuilderWithSeveralStepNameFieldNames
    {
        public SimpleBuilderWithSeveralStepNameFieldNames()
        {
            new GenerateStepwiseBuilder()
                .AddStep<int>("FirstStep", "First")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep", "Fourth")
                .CreateBuilderFor<string>();
        }
    }
    
    [StepwiseBuilder]
    public partial class SimpleBuilderWithSeveralStatementsInConstructor
    {
        public SimpleBuilderWithSeveralStatementsInConstructor()
        {
            new object();
            new object();
            new object();
            
            new GenerateStepwiseBuilder()
                .AddStep<int>("FirstStep", "First")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
            
            new object();
            new object();
            new object();
        }
    }
    
    [StepwiseBuilder]
    public partial class BuilderWithGenericParameter<T, T1> 
        where T : Exception, IList<T>
        where T1 : Exception
    {
        public BuilderWithGenericParameter()
        {
            new GenerateStepwiseBuilder()
                .AddStep<T>("FirstStep", "First")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
    }
    
    [StepwiseBuilder]
    public partial class BuilderWithGenericParameters<T1, T2, T3>
    where T1 : Exception
    where T2 : MockInterface
    {
        public BuilderWithGenericParameters()
        {
            new GenerateStepwiseBuilder()
                .AddStep<T1>("FirstStep", "First")
                .AddStep<T2>("SecondStep")
                .AddStep<T3>("ThirdStep")
                .AddStep<object>("FourthStep")
                .CreateBuilderFor<string>();
        }
    }
    
    //Will not produce builder
    public partial class SimpleBuilderWithoutAttribute
    {
        public SimpleBuilderWithoutAttribute()
        {
            new GenerateStepwiseBuilder()
                .AddStep<int>("FirstStep")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
    }
    
    //Will not produce builder
    [StepwiseBuilder]
    public partial class BuilderWithParametersInConstructor
    {
        public BuilderWithParametersInConstructor(int a1)
        {
            new GenerateStepwiseBuilder()
                .AddStep<int>("FirstStep")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
        
        public BuilderWithParametersInConstructor(int a1, int a2, int a3)
        {
            new GenerateStepwiseBuilder()
                .AddStep<int>("FirstStep")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
    }
    
    //Will not produce builder
    [StepwiseBuilder]
    public partial class BuilderWithoutCreateBuilderForCall
    {
        public BuilderWithoutCreateBuilderForCall()
        {
            new GenerateStepwiseBuilder()
                .AddStep<int>("FirstStep")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep");
        }
    }
    
    //Will not produce builder
    [StepwiseBuilder]
    public partial class BuilderWithoutAddStepCalls
    {
        public BuilderWithoutAddStepCalls()
        {
            new GenerateStepwiseBuilder()
                .CreateBuilderFor<string>();
        }
    }
    
    //Will not produce builder
    [StepwiseBuilder]
    public partial class BuilderWithEmptyConstructor
    {
        public BuilderWithEmptyConstructor()
        {

        }
    }
    
    //Produces only first builder declaration
    [StepwiseBuilder]
    public partial class BuilderWithSeveralGenerateDeclarations
    {
        public BuilderWithSeveralGenerateDeclarations()
        {
            new GenerateStepwiseBuilder()
                .AddStep<int>("FirstStep")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
            
            new GenerateStepwiseBuilder()
                .AddStep<int>("FirstStep")
                .AddStep<string>("SecondStep")
                .AddStep<object>("ThirdStep")
                .CreateBuilderFor<string>();
        }
    }
}
