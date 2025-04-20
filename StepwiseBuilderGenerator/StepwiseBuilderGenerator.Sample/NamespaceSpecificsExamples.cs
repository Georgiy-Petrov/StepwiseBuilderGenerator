using StepwiseBuilderGenerator;
using StepwiseBuilderGenerator.SampleCustomNamespace;
using System.Threading.Channels;

namespace StepwiseBuilderGenerator.NestedExample
{
    //Currently, using directives inside namespaces are not included in the generated files.
    using System.Linq;
    
    namespace NestedNamespace
    {
        using System.Threading;
        
        namespace NestedNamespace1
        {
            namespace NestedNamespace2
            {
                using System.Threading.Channels;
                
                [StepwiseBuilder]
                public partial class BuilderInsideNestedNamespace
                {
                    public BuilderInsideNestedNamespace()
                    {
                        GenerateStepwiseBuilder
                            .AddStep<string>("SomeStep")
                            .CreateBuilderFor<ChannelOptions>();
                    }
                }
            }
        }
    }
}

namespace StepwiseBuilderGenerator.SampleCustomNamespace
{
    [StepwiseBuilder]
    public partial class BuilderWithoutDeclaredSystemUsing
    {
        public BuilderWithoutDeclaredSystemUsing()
        {
            GenerateStepwiseBuilder
                .AddStep<string>("SomeStep")
                .CreateBuilderFor<string>();
        }
    }
}

[StepwiseBuilder]
public partial class BuilderInsideGlobalNamespace
{
    public BuilderInsideGlobalNamespace()
    {
        GenerateStepwiseBuilder
            .AddStep<string>("SomeStep")
            .CreateBuilderFor<string>();
    }
}

[StepwiseBuilder]
public partial class BuilderInsideGlobalNamespaceWithExternalNamespaceDependency
{
    public BuilderInsideGlobalNamespaceWithExternalNamespaceDependency()
    {
        GenerateStepwiseBuilder
            .AddStep<string>("SomeStep")
            .CreateBuilderFor<BuilderWithoutDeclaredSystemUsing>();
    }
}