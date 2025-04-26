using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StepwiseBuilderGenerator.DTOs;

namespace StepwiseBuilderGenerator;

[Generator]
public class StepwiseBuilderGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // --------------------------------------------------------------------------------
        // 1) Define a provider to gather core "BuilderInfo" data:
        //    - Scans for classes with [StepwiseBuilder] attribute
        //    - Ensures a parameterless constructor exists
        //    - Looks for a statement calling GenerateStepwiseBuilder().CreateBuilderFor<T>()
        //    - If found, extracts detailed builder configuration (e.g., steps, target type)
        // --------------------------------------------------------------------------------
        var builderInfoProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            // Metadata name of our attribute
            typeof(StepwiseBuilder).FullName!,

            // Predicate: checks if the annotated class has the necessary statements
            static (syntaxNode, _) =>
            {
                // Attempt to find a parameterless constructor
                var statements = syntaxNode
                    .TryCast<ClassDeclarationSyntax>()?
                    .Members.FirstOrDefault(static
                        member => member is ConstructorDeclarationSyntax { ParameterList.Parameters.Count: 0 }
                    )
                    ?.TryCast<ConstructorDeclarationSyntax>()?.Body?.Statements;

                // Check if there's a statement that calls 'GenerateStepwiseBuilder'
                var generateBuilderStatementCall = statements?.FirstOrDefault(static statement =>
                    statement.TryFindFirstNode<IdentifierNameSyntax>(static identifier =>
                        identifier.Identifier.Text == "GenerateStepwiseBuilder") is not null);

                // Verify the call to 'CreateBuilderFor(...)' is present
                var createBuilderForCallPresence =
                    generateBuilderStatementCall?
                        .TryCast<ExpressionStatementSyntax>()?.Expression
                        .TryCast<InvocationExpressionSyntax>()?.Expression
                        .TryCast<MemberAccessExpressionSyntax>()?
                        .Name.Identifier.Text == "CreateBuilderFor";

                // Only return true if we found statements plus the key method invocation
                return statements is not { Count: 0 }
                       && generateBuilderStatementCall is not null
                       && createBuilderForCallPresence;
            },

            // Transform: if the above predicate is true, extract the BuilderInfo details
            static (syntaxContext, _) =>
            {
                // Find the specific invocation expression for CreateBuilderFor<T>()
                var invocation = syntaxContext.TargetNode
                    .TryCast<ClassDeclarationSyntax>()!.Members.First(static member =>
                        member is ConstructorDeclarationSyntax
                        {
                            ParameterList.Parameters.Count: 0
                        })
                    .TryCast<ConstructorDeclarationSyntax>()!.Body!.Statements.First(static statement =>
                        statement.TryCast<ExpressionStatementSyntax>()?.Expression
                            .TryCast<InvocationExpressionSyntax>()?.Expression
                            .TryCast<MemberAccessExpressionSyntax>()?.Name.Identifier.Text == "CreateBuilderFor")
                    .TryCast<ExpressionStatementSyntax>()!.Expression
                    .TryCast<InvocationExpressionSyntax>();

                // Gather relevant 'using' statements
                var usings =
                    syntaxContext.SemanticModel.SyntaxTree.GetCompilationUnitRoot().Usings
                        .Select(static u => u.ToString())
                        .ToEquatableArray();

                // Extract the target type from CreateBuilderFor<T>()
                var targetType =
                    invocation?.Expression
                        .TryCast<MemberAccessExpressionSyntax>()!.Name
                        .TryCast<GenericNameSyntax>()!.TypeArgumentList.Arguments.Count == 1
                        ? invocation.Expression
                            .TryCast<MemberAccessExpressionSyntax>()!.Name
                            .TryCast<GenericNameSyntax>()!.TypeArgumentList.Arguments[0].ToString()
                        : invocation?.Expression
                            .TryCast<MemberAccessExpressionSyntax>()!.Name
                            .TryCast<GenericNameSyntax>()!.TypeArgumentList.Arguments[1].ToString();

                // Extract the defaultValueFactory for CreateBuilderFor<T>()
                (string, string)? createBuilderForInfo = null;

                if (invocation?.ArgumentList.Arguments.Count is not 0)
                {
                    var createBuilderForBuilderTypeParameter = invocation?.Expression
                        .TryCast<MemberAccessExpressionSyntax>()!.Name
                        .TryCast<GenericNameSyntax>()!.TypeArgumentList.Arguments[0].ToString();

                    var createBuilderForDefaultValueFactory =
                        invocation?.ArgumentList.Arguments.ElementAtOrDefault(0)?.Expression.ToString();

                    createBuilderForInfo = (createBuilderForBuilderTypeParameter, createBuilderForDefaultValueFactory);
                }

                // Collect all chained .AddStep(...) calls to build a step list
                var addStepInvocations =
                    invocation
                        .CollectMethodsInChain()
                        .Reverse() // ensure we process them in source order
                        .Where(static methodInfo => methodInfo.MethodName == "AddStep")
                        .Select(static (methodInfo, i) => new StepInfo(
                            Order: i,
                            StepName: methodInfo.Arguments[ArgumentType.StepName]!,
                            FieldName: methodInfo.Arguments[ArgumentType.FieldName],
                            ParameterType: methodInfo.GenericArguments!.Value.GetArray()!.Single(),
                            DefaultValueFactory: methodInfo.Arguments[ArgumentType.DefaultValueFactory]))
                        .OrderBy(static step => step.Order)
                        .ToEquatableArray();

                // Collect all chained .AndOverload(...) calls to build a overloads list
                var stepInfosOverloads =
                    invocation
                        .CollectMethodsInChain()
                        .Reverse() // ensure we process them in source order
                        .Aggregate(("", new List<StepInfoOverloadInfo>()), static (tuple, info) =>
                        {
                            var (currentStepName, stepInfoOverloadInfo) = tuple;

                            if (info.MethodName is "AddStep")
                            {
                                currentStepName = info.Arguments[ArgumentType.StepName]!;
                                return (currentStepName, stepInfoOverloadInfo);
                            }

                            if (info.MethodName is "AndOverload")
                            {
                                var stepInfoOverload = new StepInfoOverloadInfo(
                                    StepName: currentStepName,
                                    ParameterType: info.GenericArguments!.Value.GetArray()![0],
                                    ReturnType: info.GenericArguments!.Value.GetArray()![1],
                                    Mapper: info.Arguments[ArgumentType.AndOverloadMapper]!,
                                    OverloadMethodName: info.Arguments[ArgumentType.AndOverloadNewName]);
                                stepInfoOverloadInfo.Add(stepInfoOverload);

                                return (currentStepName, stepInfoOverloadInfo);
                            }

                            return (currentStepName, stepInfoOverloadInfo);
                        }).Item2
                        .ToEquatableArray();

                // Retrieve the class declaration and its info (namespace, name, constraints, etc.)
                var classDeclaration = syntaxContext.TargetNode
                    .TryCast<ClassDeclarationSyntax>();
                var className = classDeclaration!.Identifier.ToString();
                var classTypeParametersAndConstraints =
                (
                    classDeclaration.TypeParameterList?.Parameters.ToString() ?? "",
                    classDeclaration.ConstraintClauses.ToString()
                );

                // Check if there's a side path builder call: .BranchFrom<OtherBuilder>("SomeStep")
                var sidePathForBuilders =
                    invocation
                        .CollectMethodsInChain()
                        .Where(static mi => mi.MethodName == "BranchFrom")
                        .Select(static mi =>
                        {
                            var builderToExtendName =
                                string.Join("", mi.GenericArguments!.Value.GetArray()![0].TakeWhile(c => c != '<'));
                            var stepName =
                                mi.Arguments[ArgumentType.BranchFromStepName]!.ToString();

                            return new SidePathInfo(builderToExtendName, stepName);
                        })
                        .SingleOrDefault();

                var builderNamespace = syntaxContext.TargetSymbol.ContainingNamespace.ToString();

                // Build and return the info needed to generate code
                return new BuilderInfo(
                    Usings: usings,
                    TargetTypeName: targetType!,
                    StepMethods: addStepInvocations,
                    ClassName: className,
                    TypeParametersAndConstraints: classTypeParametersAndConstraints,
                    SidePath: sidePathForBuilders,
                    DeclaredNamespace: builderNamespace,
                    CreateBuilderForDefaultValueFactoryInfo: createBuilderForInfo,
                    StepInfosOverloads: stepInfosOverloads.Count is 0 ? null : stepInfosOverloads
                );
            }
        );

        // --------------------------------------------------------------------------------
        // 2) Register Source Output:
        //    - Combine the two providers: one for main builder data (builderInfoProvider) and
        //      one for extension data (extendedBuilderInfoProvider).
        //    - Pass them as a combined tuple into our source-generation method:
        //      'GenerateSourceForStepwiseBuilders'
        // --------------------------------------------------------------------------------
        context.RegisterSourceOutput(
            builderInfoProvider,
            GenerateSourceForStepwiseBuilders
        );
    }

    private static void GenerateSourceForStepwiseBuilders(
        SourceProductionContext context,
        BuilderInfo builderInfo)
    {
        // Extract the builder-to-extend name if any side-path is configured
        var builderToExtendName = builderInfo.SidePath?.BaseBuilderName;

        // Start with the usings from the original info
        var finalUsings = (builderInfo.Usings ?? Enumerable.Empty<string>()).Prepend("using System;")
            .Distinct();

        // Pull out the main elements
        var targetType = builderInfo.TargetTypeName;
        var steps = builderInfo.StepMethods;
        var @namespace = builderInfo.DeclaredNamespace == "<global namespace>"
            ? ""
            : $"namespace {builderInfo.DeclaredNamespace};";
        var (typeParams, constraints) = builderInfo.TypeParametersAndConstraints;
        var className = builderInfo.ClassName;

        // We'll accumulate all the "I{ClassName}XYZ" interfaces here
        var interfaceNames = new List<string>();

        // Prepare the generics (e.g. <T> or <T1,T2>), or null if not needed
        string? genericParams = null;
        if (!string.IsNullOrEmpty(typeParams))
        {
            genericParams = $"<{typeParams}>";
            if (genericParams == "<>")
                genericParams = null;
        }

        // Determine if provided Type parameter in CreateBuilderFor with defaultValueFactory matches builder type
        string? createBuilderInfoDefaultValueFactory = null;
        if (builderInfo.CreateBuilderForDefaultValueFactoryInfo?.Item1 == className + genericParams)
        {
            createBuilderInfoDefaultValueFactory = builderInfo.CreateBuilderForDefaultValueFactoryInfo?.Item2;
        }

        // Build the generated code in a StringBuilder
        var sourceBuilder = new StringBuilder();

        // 1) Write out all the using statements and namespace
        sourceBuilder.Append($$"""
                               {{string.Join("\n", finalUsings)}}

                               {{@namespace}}


                               """);

        // We'll reference the steps more than once, so let's store them as an array
        var stepsArray = steps.GetArray()!;
        var stepCount = stepsArray.Length;

        // 2) Generate an interface per step
        for (var i = 0; i < stepCount; i++)
        {
            var stepInfo = stepsArray[i];
            var nextStepName = i < stepCount - 1
                ? stepsArray[i + 1].StepName
                : "Build";

            var interfaceName = $"I{className}{stepInfo.StepName}{genericParams}";
            interfaceNames.Add(interfaceName);

            sourceBuilder.Append($$"""
                                   public interface {{interfaceName}} {{constraints}}
                                   {
                                       I{{className}}{{nextStepName}}{{genericParams}} {{stepInfo.StepName}}({{stepInfo.ParameterType}} value);
                                   }

                                   """);
        }

        // 3) Generate the build interface (the final interface in the chain)
        var buildInterfaceName = $"I{className}Build{genericParams}";
        interfaceNames.Add(buildInterfaceName);

        sourceBuilder.Append($$"""
                               public interface I{{className}}Build{{genericParams}} {{constraints}}
                               {
                                   {{targetType}} Build(Func<{{className}}{{genericParams}}, {{targetType}}> buildFunc);
                               }

                               """);

        // 4) Generate the partial class that implements all step-interfaces + the build-interface
        sourceBuilder.Append($$"""
                               public partial class {{className}}{{genericParams}} : {{string.Join(",", interfaceNames)}} {{constraints}}
                               {
                               """);

        if (builderToExtendName is not null)
        {
            sourceBuilder.Append($$"""
                                   
                                       public {{className}}({{builderToExtendName}}{{genericParams}} originalBuilder)
                                       {
                                           OriginalBuilder = originalBuilder;
                                       }
                                   
                                       public {{builderToExtendName}}{{genericParams}} OriginalBuilder;

                                   """);
        }

        // 5) Generate fields for each step
        foreach (var stepInfo in stepsArray)
        {
            var fieldName = stepInfo.FieldName ?? $"{stepInfo.StepName}Value";
            sourceBuilder.AppendLine($"\n    public {stepInfo.ParameterType} {fieldName};");
        }

        sourceBuilder.AppendLine();

        // 6) For each step, implement the method
        for (var i = 0; i < stepCount; i++)
        {
            var stepInfo = stepsArray[i];
            var interfaceForNext = interfaceNames[i + 1];
            var fieldName = stepInfo.FieldName ?? $"{stepInfo.StepName}Value";

            sourceBuilder.Append($$"""
                                       public {{interfaceForNext}} {{stepInfo.StepName}}({{stepInfo.ParameterType}} value)
                                       {
                                           {{fieldName}} = value;
                                           return this;
                                       }

                                   """);
        }

        // 7) Implement the Build method
        sourceBuilder.Append($$"""
                                   public {{targetType}} Build(Func<{{className}}{{genericParams}}, {{targetType}}> buildFunc)
                                   {
                                       return buildFunc(this);
                                   }
                               """);

        // 8) Include an enum of steps
        sourceBuilder.Append($$"""
                               
                                   public enum Steps
                                   {

                               """);

        foreach (var stepInfo in stepsArray)
        {
            sourceBuilder.AppendLine($"        {stepInfo.StepName},");
        }

        sourceBuilder.Append($$"""
                                   }
                               }

                               """);

        // 9) If this is base builder, Generate factory method for each non-Branch builder
        if (builderToExtendName is null && stepsArray.FirstOrDefault() is not null)
        {
            var returnType = interfaceNames[0];

            sourceBuilder.Append($$"""
                                   
                                   public static partial class StepwiseBuilders
                                   {
                                       public static {{returnType}} {{className}}{{genericParams}}() {{constraints}}
                                       {
                                            return new {{className}}{{genericParams}}();
                                       }
                                   }

                                   """);
        }

        // 10) If steps have default value factories, generate appropriate extension methods
        if (steps.Any(s => s.DefaultValueFactory is not null))
        {
            var stepsWithDefaultValueFactories =
                new LinkedList<StepInfo>(steps.Where(static s => s.DefaultValueFactory is not null)
                    .OrderBy(static s => s.Order));
            var extensionClassName = $"Init{className}Extensions";

            sourceBuilder.Append($$"""

                                   public static partial class {{extensionClassName}}
                                   {
                                   """);

            var currentStep = stepsWithDefaultValueFactories.First;

            while (currentStep is not null)
            {
                var stepInterface = interfaceNames[currentStep.Value.Order];
                
                if (builderToExtendName is not null && currentStep.Value.Order == 0)
                {
                    stepInterface = $"I{builderToExtendName}{builderInfo.SidePath!.BaseBuilderStep}";
                }
                
                sourceBuilder.Append($$"""
                                       
                                           public static {{interfaceNames[currentStep.Value.Order + 1]}} {{currentStep.Value.StepName}}{{genericParams}}(this {{stepInterface}} step) {{constraints}}
                                           {
                                               Func<{{currentStep.Value.ParameterType}}> valueFactory = {{currentStep.Value.DefaultValueFactory}};
                                               return step.{{currentStep.Value.StepName}}(valueFactory());
                                           }

                                       """);

                var start = currentStep.Value.Order;

                // 10.1) contiguous defaults from here
                var defaultsRun = stepsArray
                    .Skip(start)
                    .TakeWhile(static s => s.DefaultValueFactory is not null)
                    .ToArray();

                // 10.2) if we have more than one default, allow SkipTo for each later default
                if (defaultsRun.Length > 1)
                {
                    var sourceIface = interfaceNames[start];
                    
                    if (builderToExtendName is not null && currentStep.Value.Order == 0)
                    {
                        sourceIface = $"I{builderToExtendName}{builderInfo.SidePath!.BaseBuilderStep}";
                    }

                    foreach (var target in defaultsRun.Skip(1))
                    {
                        var targetIface = interfaceNames[target.Order + 1]; // after that step
                        var chain = string.Concat(
                            defaultsRun
                                .TakeWhile(s => s.Order < target.Order)
                                .Select(static s => $".{s.StepName}()")
                        );

                        sourceBuilder.Append($$"""
                                               
                                                   public static {{targetIface}} SkipTo{{target.StepName}}{{genericParams}}(
                                                       this {{sourceIface}} step, {{target.ParameterType}} value
                                                   ) {{constraints}}
                                                   {
                                                       return step{{chain}}.{{target.StepName}}(value);
                                                   }

                                               """);
                    }
                }

                // 10.3) now compute “next” target (either a real step, or Build if we ran out)
                if (defaultsRun.Length > 0)
                {
                    var sourceIface = interfaceNames[start];
                    var nextIndex = start + defaultsRun.Length;
                    string nextName;
                    string nextParams;
                    string nextCall;
                    string returnIface;
                    
                    if (builderToExtendName is not null && currentStep.Value.Order == 0)
                    {
                        sourceIface = $"I{builderToExtendName}{builderInfo.SidePath!.BaseBuilderStep}{genericParams}";
                    }

                    if (nextIndex < stepsArray.Length)
                    {
                        // the next non-default step
                        var nextStep = stepsArray[nextIndex];
                        nextName = nextStep.StepName;
                        nextParams = $"{nextStep.ParameterType} value";
                        nextCall = $".{nextName}(value)";
                        returnIface = interfaceNames[nextIndex + 1];
                    }
                    
                    else
                    {
                        // no steps left → Build
                        nextName = "Build";
                        nextParams = $"Func<{className}{genericParams}, {targetType}> buildFunc";
                        nextCall = ".Build(buildFunc)";
                        returnIface = targetType; // I{Class}Build<…>
                    }

                    // chain _all_ defaults so far
                    var chainAll = string.Concat(defaultsRun.Select(s => $".{s.StepName}()"));

                    sourceBuilder.Append($$"""
                                           
                                               public static {{returnIface}} SkipTo{{nextName}}{{genericParams}}(
                                                   this {{sourceIface}} step,
                                                   {{nextParams}}
                                               ) {{constraints}}
                                               {
                                                   return step{{chainAll}}{{nextCall}};
                                               }

                                           """);

                    if (builderInfo.StepInfosOverloads is not null)
                    {
                        var stepInfoOverloads = builderInfo.StepInfosOverloads.Value
                            .Where(sio => sio.StepName == nextName).ToList();

                        foreach (var stepOverload in stepInfoOverloads)
                        {
                            nextName = stepOverload.OverloadMethodName ?? stepOverload.StepName;
                            nextParams = $"{stepOverload.ParameterType} value";
                            nextCall = $".{nextName}(value)";

                            sourceBuilder.Append($$"""
                                                   
                                                       public static {{returnIface}} SkipTo{{nextName}}{{genericParams}}(
                                                           this {{sourceIface}} step,
                                                           {{nextParams}}
                                                       ) {{constraints}}
                                                       {
                                                           return step{{chainAll}}{{nextCall}};
                                                       }

                                                   """);
                        }
                    }

                    if (createBuilderInfoDefaultValueFactory is not null && nextName == "Build")
                    {
                        sourceBuilder.Append($$"""
                                               
                                                   public static {{returnIface}} SkipTo{{nextName}}{{genericParams}}(
                                                       this {{sourceIface}} step
                                                   ) {{constraints}}
                                                   {
                                                       return step{{chainAll}}.Build();
                                                   }

                                               """);
                    }
                }

                currentStep = currentStep.Next;
            }

            sourceBuilder.Append("""
                                 }
                                 """);
        }

        //11) If Build has default value factory, generate appropriate extension method
        if (createBuilderInfoDefaultValueFactory is not null)
        {
            var extensionClassName = $"Init{className}Extensions";

            sourceBuilder.Append($$"""

                                   public static partial class {{extensionClassName}}
                                   {
                                      public static {{targetType}} Build{{genericParams}}(this {{interfaceNames.Last()}} step) {{constraints}}
                                      {
                                          Func<{{className}}{{genericParams}}, {{targetType}}> valueFactory = {{createBuilderInfoDefaultValueFactory}};
                                          return step.Build(valueFactory);
                                      }
                                   }
                                   """);
        }

        // 12) If this builder extends another, generate an extension method to jump into the new builder
        if (builderToExtendName is not null)
        {
            var firstStepInfo = stepsArray[0];
            var extensionClassName = $"Init{className}Extensions";
            var extensionInterface =
                $"I{builderToExtendName}{builderInfo.SidePath!.BaseBuilderStep}{genericParams}";
            var extensionReturn = interfaceNames[1];

            sourceBuilder.Append($$"""
                                   
                                   public static partial class {{extensionClassName}}
                                   {
                                       public static {{extensionReturn}} {{firstStepInfo.StepName}}{{genericParams}}(
                                           this {{extensionInterface}} originalStep,
                                           {{firstStepInfo.ParameterType}} value
                                       ) {{constraints}}
                                       {
                                           return new {{className}}{{genericParams}}(({{builderToExtendName}}{{genericParams}})originalStep)
                                               .{{firstStepInfo.StepName}}(value);
                                       }
                                   }

                                   """);
        }

        // 13) If this builder has AndOverload, generate an extension methods with overloads for steps
        if (builderInfo.StepInfosOverloads is not null)
        {
            var extensionClassName = $"Init{className}Extensions";

            sourceBuilder.Append($$"""

                                   public static partial class {{extensionClassName}}
                                   {
                                   """);

            foreach (var stepOverload in builderInfo.StepInfosOverloads)
            {
                var stepInfo = stepsArray.First(s => s.StepName == stepOverload.StepName);
                var stepInterface = interfaceNames[stepInfo.Order];
                var extensionReturn = interfaceNames[stepInfo.Order + 1];
                var overloadName = stepOverload.OverloadMethodName ?? stepInfo.StepName;

                if (stepInfo.Order == 0 && builderToExtendName is not null)
                {
                    stepInterface = $"I{builderToExtendName}{builderInfo.SidePath!.BaseBuilderStep}{genericParams}";
                    extensionReturn = interfaceNames[1];
                }

                sourceBuilder.Append($$"""
                                           
                                           public static {{extensionReturn}} {{overloadName}}{{genericParams}}(
                                               this {{stepInterface}} originalStep,
                                               {{stepOverload.ParameterType}} value
                                           ) {{constraints}}
                                           {
                                               Func<{{stepOverload.ParameterType}}, {{stepOverload.ReturnType}}> mapper = {{stepOverload.Mapper}};
                                               return originalStep.{{stepInfo.StepName}}(mapper(value));
                                           }
                                           
                                       """);
            }

            sourceBuilder.Append($$"""
                                   
                                   }
                                   """);
        }

        // 14) Finally, add the generated code to the compilation
        var hintName = $"{className}.g.cs";
        context.AddSource(hintName, sourceBuilder.ToString());
    }
}