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
        // 1) Define an incremental provider pipeline to gather core "BuilderInfo" data:
        //    - Scans syntax trees for classes annotated with the [StepwiseBuilder] attribute.
        //    - Filters these classes to ensure they have a parameterless constructor.
        //    - Further filters to ensure the constructor contains a specific configuration chain:
        //      GenerateStepwiseBuilder()./* Steps */.CreateBuilderFor<T>()
        //    - If all criteria are met, transforms the syntax and semantic info into a BuilderInfo DTO,
        //      extracting details like target type, steps, overloads, branching, defaults, etc.
        // --------------------------------------------------------------------------------
        var builderInfoProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            typeof(StepwiseBuilder).FullName!,

            // Filter classes with [StepwiseBuilder] attribute and specific constructor pattern
            static (syntaxNode, _) =>
            {
                // Check 1: Ensure the class has a parameterless constructor.
                var statements = syntaxNode
                    .TryCast<ClassDeclarationSyntax>()?
                    .Members.FirstOrDefault(static
                        member => member is ConstructorDeclarationSyntax { ParameterList.Parameters.Count: 0 }
                    )
                    ?.TryCast<ConstructorDeclarationSyntax>()?.Body?.Statements;

                // Check 2: Ensure the constructor body contains a statement invoking 'GenerateStepwiseBuilder'.
                var generateBuilderStatementCall = statements?.FirstOrDefault(static statement =>
                    statement.TryFindFirstNode<IdentifierNameSyntax>(static identifier =>
                        identifier.Identifier.Text == "GenerateStepwiseBuilder") is not null);

                // Check 3: Ensure the 'GenerateStepwiseBuilder' call is chained with '.CreateBuilderFor(...)'.
                var createBuilderForCallPresence =
                    generateBuilderStatementCall?
                        .TryCast<ExpressionStatementSyntax>()?.Expression
                        .TryCast<InvocationExpressionSyntax>()?.Expression
                        .TryCast<MemberAccessExpressionSyntax>()?
                        .Name.Identifier.Text == "CreateBuilderFor";

                return statements is not { Count: 0 }
                       && generateBuilderStatementCall is not null
                       && createBuilderForCallPresence;
            },

            // Extract builder configuration from valid classes
            static (syntaxContext, _) =>
            {
                // Locate the specific 'GenerateStepwiseBuilder().CreateBuilderFor<T>(...)' invocation expression within the constructor.
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

                var constructorsParameters = syntaxContext.TargetNode
                    .TryCast<ClassDeclarationSyntax>()!.Members.Where(static member =>
                        member is ConstructorDeclarationSyntax).Select(constructor =>
                        constructor.TryCast<ConstructorDeclarationSyntax>()!.ParameterList.Parameters
                            .Select(p => p.ToString()).ToEquatableArray()).ToEquatableArray();

                // Gather all 'using' directives from the source file containing the builder configuration.
                var usings =
                    syntaxContext.SemanticModel.SyntaxTree.GetCompilationUnitRoot().Usings
                        .Select(static u => u.ToString())
                        .ToEquatableArray();

                // Extract the target type 'T' being built from the 'CreateBuilderFor<T>()' generic arguments.
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

                // Check for optional arguments passed to CreateBuilderFor, typically representing a default build function factory.
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

                // Collect all chained '.AddStep(...)' calls representing mandatory builder steps.
                var addStepInvocations =
                    invocation
                        .CollectMethodsInChain() // Assumes CollectMethodsInChain gets methods bottom-up;
                        .Reverse() // Reverse to process in source code order.
                        .Where(static methodInfo => methodInfo.MethodName == "AddStep")
                        .Where(static methodInfo => methodInfo.Arguments[ArgumentType.DefaultValueFactory] is null)
                        .Select(static (methodInfo, i) => new StepInfo(
                            Order: i,
                            StepName: methodInfo.Arguments[ArgumentType.StepName]!,
                            FieldName: methodInfo.Arguments[ArgumentType.FieldName],
                            ParameterType: methodInfo.GenericArguments!.Value.GetArray()!.Single(),
                            DefaultValueFactory: null))
                        .OrderBy(static step => step.Order)
                        .ToEquatableArray();

                // Collect AddStep calls that *also* provide a default value factory argument for optional initialization.
                var addStepInvocationsWithDefaultValueFactory =
                    invocation
                        .CollectMethodsInChain() // Assumes CollectMethodsInChain gets methods bottom-up;
                        .Reverse() // Reverse to process in source code order.
                        // Filter for AddStep calls that *do* provide a default value factory argument.
                        .Where(static methodInfo => methodInfo.MethodName == "AddStep")
                        .Where(static methodInfo => methodInfo.Arguments[ArgumentType.DefaultValueFactory] is not null)
                        .Select(static (methodInfo, i) => new StepInfo(
                            Order: i,
                            StepName: methodInfo.Arguments[ArgumentType.StepName]!,
                            FieldName: methodInfo.Arguments[ArgumentType.FieldName],
                            ParameterType: methodInfo.GenericArguments!.Value.GetArray()!.Single(),
                            DefaultValueFactory: methodInfo.Arguments[ArgumentType.DefaultValueFactory]))
                        .OrderBy(static step => step.Order)
                        .ToEquatableArray();

                // Collect all chained '.AndOverload(...)' calls, which define alternative methods for providing step values.
                var stepInfosOverloads =
                    invocation
                        .CollectMethodsInChain() // Assumes CollectMethodsInChain gets methods bottom-up;
                        .Reverse() // Reverse to process in source code order.
                        // Use Aggregate to process the chain, remembering the last 'AddStep' encountered
                        // to correctly associate subsequent 'AndOverload' calls with their respective step.
                        .Aggregate(("", new List<StepInfoOverloadInfo>()), static (tuple, info) =>
                        {
                            var (currentStepName, stepInfoOverloadInfo) = tuple;

                            if (info.MethodName is "AddStep")
                            {
                                // Remember the current step when encountered
                                currentStepName = info.Arguments[ArgumentType.StepName]!;
                                return (currentStepName, stepInfoOverloadInfo);
                            }

                            if (info.MethodName is "AndOverload")
                            {
                                // Create overload info associated with the current step
                                var stepInfoOverload = new StepInfoOverloadInfo(
                                    StepName: currentStepName, // Associate with the last seen AddStep
                                    ParameterType: info.GenericArguments!.Value.GetArray()![0],
                                    ReturnType: info.GenericArguments!.Value.GetArray()![1],
                                    Mapper: info.Arguments[ArgumentType.AndOverloadMapper]!,
                                    OverloadMethodName: info.Arguments[ArgumentType.AndOverloadNewName]);
                                stepInfoOverloadInfo.Add(stepInfoOverload);

                                return (currentStepName, stepInfoOverloadInfo);
                            }

                            // Ignore other methods in the chain for overload processing
                            return (currentStepName, stepInfoOverloadInfo);
                        }).Item2 // Get the populated list of overloads
                        .ToEquatableArray();

                // Retrieve details about the configuration class itself (name, namespace, generics, constraints).
                var classDeclaration = syntaxContext.TargetNode
                    .TryCast<ClassDeclarationSyntax>();
                var className = classDeclaration!.Identifier.ToString();
                var classTypeParametersAndConstraints =
                (
                    classDeclaration.TypeParameterList?.Parameters.ToString() ?? "",
                    classDeclaration.ConstraintClauses.ToString()
                );

                // Check if the configuration includes a '.BranchFromStepBefore<OtherBuilder>(...)' call to extend another builder.
                var sidePathForBuilders =
                    invocation
                        .CollectMethodsInChain()
                        .Where(static mi => mi.MethodName == "BranchFromStepBefore")
                        .Select(static mi =>
                        {
                            // Extract the name of the builder to branch from
                            var builderToExtendName =
                                string.Join("", mi.GenericArguments!.Value.GetArray()![0].TakeWhile(c => c != '<'));
                            // Extract the step name in the base builder where branching occurs
                            var stepName =
                                mi.Arguments[ArgumentType.BranchFromStepBeforeStepName]!.ToString();

                            return new SidePathInfo(builderToExtendName, stepName);
                        })
                        .SingleOrDefault(); // Assuming only one BranchFromStepBefore call per builder

                var builderNamespace = syntaxContext.TargetSymbol.ContainingNamespace.ToString();

                // Construct the final BuilderInfo DTO containing all extracted configuration data.
                return new BuilderInfo(
                    Usings: usings,
                    TargetTypeName: targetType!,
                    ContainingTypeConstructorsParameters: constructorsParameters,
                    StepMethods: addStepInvocations,
                    StepMethodsWithDefaultValueFactory: addStepInvocationsWithDefaultValueFactory,
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
        // 2) Register Source Output Pipeline:
        //    Connects the builderInfoProvider (which produces BuilderInfo objects)
        //    to the code generation logic in 'GenerateSourceForStepwiseBuilders'.
        //    This method will be called whenever a valid BuilderInfo is produced or updated.
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
        // Extract the name of the base builder if this builder is configured to branch from another.
        var builderToExtendName = builderInfo.SidePath?.BaseBuilderName;

        // Consolidate usings from the original file, ensure 'System' is included, and remove duplicates.
        var finalUsings = (builderInfo.Usings ?? Enumerable.Empty<string>()).Prepend("using System;")
            .Distinct();

        // Extract key properties from BuilderInfo for easier access.
        var targetType = builderInfo.TargetTypeName;
        var steps = builderInfo.StepMethods;
        var @namespace = builderInfo.DeclaredNamespace == "<global namespace>"
            ? ""
            : $"namespace {builderInfo.DeclaredNamespace};";
        var (typeParams, constraints) = builderInfo.TypeParametersAndConstraints;
        var className = builderInfo.ClassName;

        // Stores the generated interface names (e.g., "IMyBuilderStep1", "IMyBuilderStep2", "IMyBuilderBuild").
        var interfaceNames = new List<string>();

        // Format the generic type parameters string (e.g., "<T>" or "<TKey, TValue>"), handling the no-generics case.
        string? genericParams = null;
        if (!string.IsNullOrEmpty(typeParams))
        {
            genericParams = $"<{typeParams}>";
            if (genericParams == "<>") // Handle empty generics case like MyBuilder<>
                genericParams = null;
        }

        // Check if a default build factory was provided via CreateBuilderFor's argument AND if its specified builder type matches this builder.
        string? createBuilderInfoDefaultValueFactory = null;
        if (builderInfo.CreateBuilderForDefaultValueFactoryInfo?.Item1 == className + genericParams)
        {
            // Use the default factory only if the type parameter matches the current builder class
            createBuilderInfoDefaultValueFactory = builderInfo.CreateBuilderForDefaultValueFactoryInfo?.Item2;
        }

        // Use a StringBuilder for efficient construction of the generated source code.
        var sourceBuilder = new StringBuilder();

        // 1) Generate 'using' directives and namespace declaration.
        sourceBuilder.Append($$"""
                               {{string.Join("\n", finalUsings)}}

                               {{@namespace}}


                               """);

        // Cache the mandatory steps array for multiple accesses.
        var stepsArray = steps.GetArray()!;
        var stepCount = stepsArray.Length;

        // 2) Generate a step-specific interface for each mandatory step.
        for (var i = 0; i < stepCount; i++)
        {
            var stepInfo = stepsArray[i];
            // Determine the name of the *next* step's interface, or the 'Build' interface if it's the last step.
            var nextStepName = i < stepCount - 1
                ? stepsArray[i + 1].StepName
                : "Build";

            var interfaceName = $"I{className}{stepInfo.StepName}{genericParams}";
            interfaceNames.Add(interfaceName); // Store for later use (class implementation)

            // Generate the interface definition
            sourceBuilder.Append($$"""
                                   public interface {{interfaceName}} {{constraints}}
                                   {
                                       // Method signature for this step, returning the next step's interface
                                       I{{className}}{{nextStepName}}{{genericParams}} {{stepInfo.StepName}}({{stepInfo.ParameterType}} value);
                                   }

                                   """);
        }

        // 3) Generate the final 'Build' interface, returned by the last step.
        var buildInterfaceName = $"I{className}Build{genericParams}";
        interfaceNames.Add(buildInterfaceName); // Add the build interface name

        sourceBuilder.Append($$"""
                               public interface {{buildInterfaceName}} {{constraints}}
                               {
                                   // Final Build method signature, taking a function to construct the target object
                                   {{targetType}} Build(Func<{{className}}{{genericParams}}, {{targetType}}> buildFunc);
                               }

                               """);

        // 4) Generate the optional 'Default Values Builder' interface if any steps have defaults.
        var stepsWithDefaultValues = builderInfo.StepMethodsWithDefaultValueFactory?.GetArray();

        var nextStepNameToReturnInStepWithDefaultValue = "TStepToReturn";
        var interfacesForDefaultSteps = new List<string>();

        if (stepsWithDefaultValues?.Length is not 0)
        {
            foreach (var stepInfo in stepsWithDefaultValues)
            {
                var interfaceName = $"I{className}{stepInfo.StepName}{genericParams}";
                interfacesForDefaultSteps.Add(interfaceName);

                sourceBuilder.Append($$"""
                                       public interface {{interfaceName}} {{constraints}}
                                       {
                                           {{nextStepNameToReturnInStepWithDefaultValue}} {{stepInfo.StepName}}<{{nextStepNameToReturnInStepWithDefaultValue}}>({{stepInfo.ParameterType}} value);
                                       }

                                       """);
            }
        }


        // 5) Generate the partial class definition for the builder implementation.
        //    This class implements all generated step interfaces, the build interface, and the optional defaults interface.
        sourceBuilder.Append($$"""

                               // Partial class implementing the generated interfaces
                               public partial class {{className}}{{genericParams}} : {{string.Join(", ", interfaceNames.Concat(interfacesForDefaultSteps))}} {{constraints}}
                               {
                               """);

        // If this builder branches from another, add a constructor accepting the base builder and a field to store it.
        if (builderToExtendName is not null)
        {
            sourceBuilder.Append($$"""
                                   
                                       // Constructor for branched builders, accepting the original builder instance
                                       public {{className}}({{builderToExtendName}}{{genericParams}} originalBuilder)
                                       {
                                           OriginalBuilder = originalBuilder;
                                       }
                                   
                                       // Field to hold the reference to the original builder
                                       public {{builderToExtendName}}{{genericParams}} OriginalBuilder;
                                   """);
        }

        // 6) Generate public fields within the partial class to store the value for each step (mandatory and default-value steps).
        foreach (var stepInfo in stepsArray.Concat(builderInfo.StepMethodsWithDefaultValueFactory ?? []))
        {
            // Use provided FieldName or generate one from StepName
            var fieldName = stepInfo.FieldName ?? $"{stepInfo.StepName}Value";
            sourceBuilder.AppendLine(
                $"\n    public {stepInfo.ParameterType} {fieldName}; // Field to store value for step '{stepInfo.StepName}'");
        }

        sourceBuilder.AppendLine();

        // 7) Implement the interface methods for each mandatory step.
        for (var i = 0; i < stepCount; i++)
        {
            var stepInfo = stepsArray[i];
            var interfaceForNext = interfaceNames[i + 1]; // Get the next interface name
            var fieldName = stepInfo.FieldName ?? $"{stepInfo.StepName}Value"; // Get the corresponding field name

            // Implement the method for this step
            sourceBuilder.Append($$"""
                                       public {{interfaceForNext}} {{stepInfo.StepName}}({{stepInfo.ParameterType}} value)
                                       {
                                           this.{{fieldName}} = value; // Store the value
                                           return this; // Return the builder for fluent chaining
                                       }

                                   """);
        }

        // 8) Implement the interface methods for steps with default values (part of the optional defaults interface).
        if (stepsWithDefaultValues is not null) // Check if there are any default steps
        {
            foreach (var stepWithDefaultValue in stepsWithDefaultValues)
            {
                var fieldName = stepWithDefaultValue.FieldName ?? $"{stepWithDefaultValue.StepName}Value";

                // Implement the method for setting the default value
                sourceBuilder.Append($$"""
                                           public {{nextStepNameToReturnInStepWithDefaultValue}} {{stepWithDefaultValue.StepName}}<{{nextStepNameToReturnInStepWithDefaultValue}}>({{stepWithDefaultValue.ParameterType}} value)
                                           {
                                               this.{{fieldName}} = value; // Store the value
                                               return ({{nextStepNameToReturnInStepWithDefaultValue}})(object)this;
                                           }

                                       """);
            }
        }

        // 9) Implement the 'Build' method from the final build interface.
        sourceBuilder.Append($$"""
                                   public {{targetType}} Build(Func<{{className}}{{genericParams}}, {{targetType}}> buildFunc)
                                   {
                                       // Execute the user-provided function to construct the target object
                                       return buildFunc(this);
                                   }

                               """);

        // 10) Implement the 'Initialize' method, used by the static factory.
        //     This applies any default value factories configured via 'AddStep'.
        if (stepsWithDefaultValues is not null) // Check needed again for variable scoping
        {
            var defaultValuesFactoriesVariables = string.Join("\n",
                stepsWithDefaultValues.Select(s =>
                    $"        Func<{s.ParameterType}> {s.StepName}DefaultValueFactory = {s.DefaultValueFactory}; // Define factory delegate variable"));
            var defaultValuesFactoriesCallChain = string.Join("\n",
                stepsWithDefaultValues.Select(s =>
                    $"        this.{s.StepName}<{interfaceNames[0]}>({s.StepName}DefaultValueFactory());")); // Chain calls to set defaults

            if (defaultValuesFactoriesCallChain == ".") // Handle case where there are no default steps
            {
                defaultValuesFactoriesCallChain = "";
            }

            sourceBuilder.Append($$"""
                                   
                                       // Initializes the builder and applies default value factories
                                       public {{interfaceNames[0]}} Initialize()
                                       {
                                   {{defaultValuesFactoriesVariables}}

                                   {{defaultValuesFactoriesCallChain}}
                                   
                                           // Apply defaults and cast to the first mandatory step interface
                                           return ({{interfaceNames[0]}})this;
                                       }

                                   """);
        }
        else // If no default values, Initialize is simpler
        {
            sourceBuilder.Append($$"""
                                   
                                       // Initializes the builder (no default value factories to apply)
                                       public {{interfaceNames[0]}} Initialize()
                                       {
                                           return ({{interfaceNames[0]}})this;
                                       }

                                   """);
        }


        // 11) Generate a public 'Steps' enum listing all mandatory step names.
        sourceBuilder.Append($$"""
                               
                                   // Enum providing names of the mandatory steps
                                   public enum Steps
                                   {

                               """);

        foreach (var stepInfo in stepsArray)
        {
            sourceBuilder.AppendLine($"        {stepInfo.StepName},"); // Add each step name as an enum member
        }

        sourceBuilder.Append($$"""
                                   }
                               }

                               """);

        // 12) If this is a root builder (not branching), generate the static factory method
        //     (e.g., `StepwiseBuilders.MyBuilder<T>()`) as the primary entry point.
        if (builderToExtendName is null)
        {
            var returnType = interfaceNames[0]; // The first step's interface

            sourceBuilder.Append($$"""

                                   // Static class containing factory methods for root builders
                                   public static partial class StepwiseBuilders
                                   {

                                   """);

            foreach (var parameters in builderInfo.ContainingTypeConstructorsParameters)
            {
                sourceBuilder.Append($$"""
                                           // Factory method to start building a {{className}}
                                           public static {{returnType}} {{className}}{{genericParams}}({{string.Join(", ", parameters)}}) {{constraints}}
                                           {
                                                // Create, initialize (apply defaults), and return the builder
                                                return new {{className}}{{genericParams}}({{string.Join(", ", parameters.Select(p => p?.Split(' ')[1]))}}).Initialize();
                                           }

                                       """);
            }

            sourceBuilder.Append($$"""
                                   }

                                   """);
        }

        // 13) Generate extension methods for steps with default values
        // This section creates a static extension class that provides convenience methods
        // for all builder steps that have default values. These extension methods allow
        // fluent builder pattern usage without explicitly passing type parameters.
        //
        // For each step with a default value, it generates extension methods for all
        // step interfaces in the builder chain.
        if (stepsWithDefaultValues?.Length is not 0)
        {
            var extensionClassName = $"Init{className}Extensions"; // Naming convention for extension class

            sourceBuilder.Append($$"""

                                   // Extension methods for {{className}} builder
                                   public static partial class {{extensionClassName}}
                                   {
                                   """);

            foreach (var stepWithDefaultValue in stepsWithDefaultValues)
            {
                foreach (var stepInterface in interfaceNames)
                {
                    sourceBuilder.Append($$"""
                                           
                                               public static {{stepInterface}} {{stepWithDefaultValue.StepName}}{{genericParams}}(this {{stepInterface}} builder, {{stepWithDefaultValue.ParameterType}} value) {{constraints}}
                                               {
                                                   return ({{stepInterface}})((({{className}}{{genericParams}})(object)builder).{{stepWithDefaultValue.StepName}}<{{stepInterface}}>(value));
                                               }

                                           """);
                }
            }

            sourceBuilder.Append($$"""
                                   }

                                   """);
        }

        // 14) If a default build function factory was provided to CreateBuilderFor, generate
        //     an extension method for a simplified `Build()` call on the final step interface.
        if (createBuilderInfoDefaultValueFactory is not null)
        {
            var extensionClassName = $"Init{className}Extensions";

            sourceBuilder.Append($$"""

                                   // Extension methods for {{className}} builder
                                   public static partial class {{extensionClassName}}
                                   {
                                      // Simplified Build() extension method using the default factory
                                      public static {{targetType}} Build{{genericParams}}(this {{interfaceNames.Last()}} step) {{constraints}}
                                      {
                                          // Define the factory delegate using the configured string
                                          Func<{{className}}{{genericParams}}, {{targetType}}> valueFactory = {{createBuilderInfoDefaultValueFactory}};
                                          // Call the main Build method with the default factory
                                          return step.Build(valueFactory);
                                      }
                                   }
                                   """);
        }

        // 15) If this builder branches from another, generate an extension method on the *base* builder's
        //     relevant step interface to allow transitioning *into* this branched builder.
        if (builderToExtendName is not null && stepsArray.Length is not 0)
        {
            var firstStepInfo = stepsArray[0];
            var extensionClassName = $"Init{className}Extensions"; // Extensions specific to the *branched* builder
            // Determine the interface of the base builder at the branching point
            var extensionInterface =
                $"I{builderToExtendName}{builderInfo.SidePath!.BaseBuilderStep}{genericParams}";
            // The return type is the *second* interface of the *branched* builder (after the first step is applied)
            var extensionReturn = interfaceNames[1];

            sourceBuilder.Append($$"""

                                   // Extension methods for {{className}} builder
                                   public static partial class {{extensionClassName}}
                                   {
                                       // Extension method on the base builder to start the '{{className}}' branch
                                       public static {{extensionReturn}} {{firstStepInfo.StepName}}{{genericParams}}(
                                           this {{extensionInterface}} originalStep, // Applied to the base builder's interface
                                           {{firstStepInfo.ParameterType}} value // Takes the first step's parameter
                                       ) {{constraints}}
                                       {
                                           // Create the branched builder, passing the original, and call its first step method
                                           return new {{className}}{{genericParams}}(({{builderToExtendName}}{{genericParams}})originalStep)
                                               .Initialize()
                                               .{{firstStepInfo.StepName}}(value);
                                       }
                                   }

                                   """);
        }

        // 16) If step overloads were defined using 'AndOverload', generate corresponding extension methods.
        if (builderInfo.StepInfosOverloads is not null)
        {
            var extensionClassName = $"Init{className}Extensions";

            sourceBuilder.Append($$"""

                                   // Extension methods for {{className}} builder
                                   public static partial class {{extensionClassName}}
                                   {
                                   """);

            // Generate overloads for standard steps (those without default value factories).
            foreach (var stepOverload in builderInfo.StepInfosOverloads?.Where(sio =>
                         !builderInfo.StepMethodsWithDefaultValueFactory?.Any(si => si.StepName == sio.StepName) ??
                         true) ?? []) // Ensure StepMethodsWithDefaultValueFactory is not null before Any()
            {
                var stepInfo =
                    stepsArray.First(s => s.StepName == stepOverload.StepName); // Find the original step info
                var stepInterface = interfaceNames[stepInfo.Order]; // Interface the overload extends
                var extensionReturn = interfaceNames[stepInfo.Order + 1]; // Interface returned by the overload
                var overloadName =
                    stepOverload.OverloadMethodName ?? stepInfo.StepName; // Use custom name or default step name

                // Special case: Handle the first step overload for a *branched* builder - apply it to the *base* builder's interface.
                if (stepInfo.Order == 0 && builderToExtendName is not null)
                {
                    stepInterface = $"I{builderToExtendName}{builderInfo.SidePath!.BaseBuilderStep}{genericParams}";
                    // Return type remains the second interface of the branched builder
                    extensionReturn = interfaceNames[1];
                }

                sourceBuilder.Append($$"""
                                       
                                           // Overload for step '{{stepInfo.StepName}}' accepting '{{stepOverload.ParameterType}}'
                                           public static {{extensionReturn}} {{overloadName}}{{genericParams}}(
                                               this {{stepInterface}} originalStep,
                                               {{stepOverload.ParameterType}} value
                                           ) {{constraints}}
                                           {
                                               // Define the mapping function
                                               Func<{{stepOverload.ParameterType}}, {{stepOverload.ReturnType}}> mapper = {{stepOverload.Mapper}};
                                               // Call the original step method with the mapped value
                                               return originalStep.{{stepInfo.StepName}}(mapper(value));
                                           }

                                       """);
            }

            // Generate overloads for steps that *do* have default value factories.
            // These overloads operate on the default values interface (e.g., IMyBuilderDefaultValuesBuilder).
            if (stepsWithDefaultValues is not null)
            {
                foreach (var stepOverload in builderInfo.StepInfosOverloads?.Where(sio =>
                             builderInfo.StepMethodsWithDefaultValueFactory?.Any(si => si.StepName == sio.StepName) ??
                             false) ?? []) // Ensure StepMethodsWithDefaultValueFactory is not null
                {
                    // Find original step info (among those with defaults) - Note: Order isn't relevant here as they apply to the same interface
                    var stepInfo = stepsWithDefaultValues.First(s => s.StepName == stepOverload.StepName);
                    var overloadName =
                        stepOverload.OverloadMethodName ?? stepInfo.StepName; // Use custom or default name

                    foreach (var stepInterface in interfaceNames)
                    {
                        sourceBuilder.Append($$"""
                                               
                                                   public static {{stepInterface}} {{overloadName}}{{genericParams}}(this {{stepInterface}} builder, {{stepOverload.ParameterType}} value) {{constraints}}
                                                   {
                                                       // Define the mapping function
                                                       Func<{{stepOverload.ParameterType}}, {{stepOverload.ReturnType}}> mapper = {{stepOverload.Mapper}};
                                                       // Call the original step method (on the defaults interface) with the mapped value
                                                       return (({{className}}{{genericParams}})(object)builder).{{stepInfo.StepName}}<{{stepInterface}}>(mapper(value));
                                                   }
                                               """);
                    }
                }
            }

            sourceBuilder.Append($$"""

                                   }
                                   """);
        }

        // 17) Finally, add the complete generated source code string to the compilation context.
        var hintName = $"{className}.g.cs";
        context.AddSource(hintName, sourceBuilder.ToString());
    }
}