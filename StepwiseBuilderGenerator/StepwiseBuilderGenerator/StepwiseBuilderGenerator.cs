using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StepwiseBuilderGenerator.DTOs;
using StepwiseBuilderGenerator.HelpersForCache;

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
                    .Members.FirstOrDefault(
                        member => member is ConstructorDeclarationSyntax { ParameterList.Parameters.Count: 0 }
                    )
                    ?.TryCast<ConstructorDeclarationSyntax>()?.Body?.Statements;

                // Check if there's a statement that calls 'GenerateStepwiseBuilder()'
                var generateBuilderStatementCall = statements?.FirstOrDefault(statement =>
                    statement.TryFindFirstNode<ObjectCreationExpressionSyntax>()?
                        .Type.TryCast<IdentifierNameSyntax>()?.Identifier.Text == "GenerateStepwiseBuilder");

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
                    .TryCast<ClassDeclarationSyntax>()!.Members.First(member => member is ConstructorDeclarationSyntax)
                    .TryCast<ConstructorDeclarationSyntax>()!.Body!.Statements.First(static statement =>
                        statement.TryCast<ExpressionStatementSyntax>()?.Expression
                            .TryCast<InvocationExpressionSyntax>()?.Expression
                            .TryCast<MemberAccessExpressionSyntax>()?.Name.Identifier.Text == "CreateBuilderFor")
                    .TryCast<ExpressionStatementSyntax>()!.Expression
                    .TryCast<InvocationExpressionSyntax>();

                // Gather relevant 'using' statements from ancestor nodes
                var usings =
                    invocation?.Ancestors()
                        .Select(static ancestor =>
                            ancestor.TryCast<BaseNamespaceDeclarationSyntax>()?.Usings ??
                            ancestor.TryCast<CompilationUnitSyntax>()?.Usings)
                        .Where(static u => u is not null)
                        .SelectMany(static list => list!.Value)
                        .OrderBy(static syntax => syntax.Name.ToString().Length)
                        .Select(static u => u.ToString())
                        .Prepend("using System;")
                        .Distinct()
                        .ToEquatableArray();

                // Extract the target type from CreateBuilderFor<T>()
                var targetType =
                    invocation?.Expression
                        .TryCast<MemberAccessExpressionSyntax>()!.Name
                        .TryCast<GenericNameSyntax>()!.TypeArgumentList.Arguments[0].ToString();

                // Collect all chained .AddStep(...) calls to build a step list
                var addStepInvocations =
                    invocation
                        .CollectMethodsInChain()
                        .Reverse() // ensure we process them in source order
                        .Where(static methodInfo => methodInfo.MethodName == "AddStep")
                        .Select(static (methodInfo, i) => new StepInfo(
                            Order: int.Parse(i.ToString()),
                            StepName: methodInfo.Arguments!.Value.GetArray()![0],
                            FieldName: methodInfo.Arguments!.Value.GetArray()!.ElementAtOrDefault(1),
                            ParameterType: methodInfo.GenericArguments!.Value.GetArray()!.Single()))
                        .OrderBy(static step => step.Order)
                        .ToEquatableArray();

                // Retrieve the class declaration and its info (namespace, name, constraints, etc.)
                var classDeclaration = GetClassDeclaration(invocation);
                var className = classDeclaration!.Identifier.ToString();
                var classTypeParametersAndConstraints =
                (
                    classDeclaration.TypeParameterList?.Parameters.ToString() ?? "",
                    classDeclaration.ConstraintClauses.ToString()
                );

                // Check if there's a side path builder call: .BranchFrom("OtherBuilder", "SomeStep")
                var sidePathForBuilders =
                    invocation
                        .CollectMethodsInChain()
                        .Where(static mi => mi.MethodName == "BranchFrom")
                        .Select(static mi =>
                        {
                            var builderToExtendName =
                                mi.Arguments!.Value.GetArray()?[0];
                            var stepName =
                                mi.Arguments!.Value.GetArray()!.ElementAtOrDefault(1);

                            return builderToExtendName is not null
                                ? new SidePathInfo(builderToExtendName, stepName!)
                                : null;
                        })
                        .SingleOrDefault();

                var builderNamespace = GetNamespace(classDeclaration);

                // Build and return the info needed to generate code
                return new BuilderInfo(
                    Usings: usings,
                    TargetTypeName: targetType!,
                    StepMethods: addStepInvocations,
                    ClassName: className,
                    TypeParametersAndConstraints: classTypeParametersAndConstraints,
                    SidePath: sidePathForBuilders,
                    DeclaredNamespace: builderNamespace
                );
            }
        );

        // --------------------------------------------------------------------------------
        // 2) Define a provider for "ExtendedBuilderInfo":
        //    - Similar logic as above, still looking for [StepwiseBuilder]-annotated classes
        //    - Gathers minimal data (namespace, class name, usings) to support extensions
        // --------------------------------------------------------------------------------
        var extendedBuilderInfoProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            typeof(StepwiseBuilder).FullName!,

            // Predicate: again looks for a parameterless constructor and "GenerateStepwiseBuilder().CreateBuilderFor(...)"
            static (syntaxNode, _) =>
            {
                var statements = syntaxNode
                    .TryCast<ClassDeclarationSyntax>()?
                    .Members.FirstOrDefault(
                        member => member is ConstructorDeclarationSyntax { ParameterList.Parameters.Count: 0 }
                    )
                    ?.TryCast<ConstructorDeclarationSyntax>()?.Body?.Statements;

                var generateBuilderStatementCall = statements?.FirstOrDefault(statement =>
                    statement.TryFindFirstNode<ObjectCreationExpressionSyntax>()?
                        .Type.TryCast<IdentifierNameSyntax>()?.Identifier.Text == "GenerateStepwiseBuilder");

                var createBuilderForCallPresence =
                    generateBuilderStatementCall?
                        .TryCast<ExpressionStatementSyntax>()?.Expression
                        .TryCast<InvocationExpressionSyntax>()?.Expression
                        .TryCast<MemberAccessExpressionSyntax>()?
                        .Name.Identifier.Text == "CreateBuilderFor";

                // Same condition as before, but we only want to gather limited extension info
                return statements is not { Count: 0 }
                       && generateBuilderStatementCall is not null
                       && createBuilderForCallPresence;
            },

            // Transform: build a simple ExtendedBuilderInfo record
            static (syntaxContext, _) =>
            {
                var invocation = syntaxContext.TargetNode
                    .TryCast<ClassDeclarationSyntax>()?.Members
                    .FirstOrDefault(member => member is ConstructorDeclarationSyntax)?
                    .TryCast<ConstructorDeclarationSyntax>()?.Body?.Statements.FirstOrDefault(static statement =>
                        statement.TryCast<ExpressionStatementSyntax>()?.Expression
                            .TryCast<InvocationExpressionSyntax>()?.Expression
                            .TryCast<MemberAccessExpressionSyntax>()?.Name.Identifier.Text == "CreateBuilderFor")
                    ?.TryCast<ExpressionStatementSyntax>()?.Expression
                    .TryCast<InvocationExpressionSyntax>();

                var classDeclaration = GetClassDeclaration(invocation);
                var builderNamespace = GetNamespace(classDeclaration!);

                // Gather 'using' statements to ensure consistent imports if we extend from this builder
                var usings =
                    invocation?.Ancestors()
                        .Select(static ancestor =>
                            ancestor.TryCast<BaseNamespaceDeclarationSyntax>()?.Usings ??
                            ancestor.TryCast<CompilationUnitSyntax>()?.Usings)
                        .Where(static u => u is not null)
                        .SelectMany(static list => list!.Value)
                        .OrderBy(static syntax => syntax.Name.ToString().Length)
                        .Select(static u => u.ToString())
                        .Prepend("using System;")
                        .Distinct()
                        .ToEquatableArray();

                // Construct the ExtendedBuilderInfo
                return new ExtendedBuilderInfo(
                    DeclaredNamespace: builderNamespace,
                    ClassName: classDeclaration!.Identifier.ToString(),
                    Usings: usings
                );
            }
        );

        // --------------------------------------------------------------------------------
        // 3) Register Source Output:
        //    - Combine the two providers: one for main builder data (builderInfoProvider) and
        //      one for extension data (extendedBuilderInfoProvider).
        //    - Pass them as a combined tuple into our source-generation method:
        //      'GenerateSourceForStepwiseBuilders'
        // --------------------------------------------------------------------------------
        context.RegisterSourceOutput(
            builderInfoProvider.Combine(extendedBuilderInfoProvider.Collect()),
            GenerateSourceForStepwiseBuilders
        );
    }


    private static ClassDeclarationSyntax? GetClassDeclaration(InvocationExpressionSyntax? invocation)
    {
        if (invocation is null)
        {
            return null;
        }

        var parent = invocation.Parent;

        while (parent is not ClassDeclarationSyntax)
        {
            parent = parent?.Parent;
        }

        return (ClassDeclarationSyntax)parent;
    }

    private static string GetNamespace(ClassDeclarationSyntax syntax)
    {
        // If we don't have a namespace at all we'll return an empty string
        // This accounts for the "default namespace" case
        string nameSpace = string.Empty;

        // Get the containing syntax node for the type declaration
        // (could be a nested type, for example)
        SyntaxNode? potentialNamespaceParent = syntax.Parent;

        // Keep moving "out" of nested classes etc until we get to a namespace
        // or until we run out of parents
        while (potentialNamespaceParent != null &&
               potentialNamespaceParent is not NamespaceDeclarationSyntax
               && potentialNamespaceParent is not FileScopedNamespaceDeclarationSyntax)
        {
            potentialNamespaceParent = potentialNamespaceParent.Parent;
        }

        // Build up the final namespace by looping until we no longer have a namespace declaration
        if (potentialNamespaceParent is BaseNamespaceDeclarationSyntax namespaceParent)
        {
            // We have a namespace. Use that as the type
            nameSpace = namespaceParent.Name.ToString();

            // Keep moving "out" of the namespace declarations until we 
            // run out of nested namespace declarations
            while (true)
            {
                if (namespaceParent.Parent is not NamespaceDeclarationSyntax parent)
                {
                    break;
                }

                // Add the outer namespace as a prefix to the final namespace
                nameSpace = $"{namespaceParent.Name}.{nameSpace}";
                namespaceParent = parent;
            }
        }

        // return the final namespace
        return nameSpace;
    }

    private static void GenerateSourceForStepwiseBuilders(
        SourceProductionContext context,
        (BuilderInfo BuilderInfo, ImmutableArray<ExtendedBuilderInfo> ExtendedBuilderInfos) generationData)
    {
        // Deconstruct the tuple for clarity
        var (builderInfo, extendedBuilderInfos) = generationData;

        // Extract the builder-to-extend name if any side-path is configured
        var builderToExtendName = builderInfo.SidePath?.BaseBuilderName;

        // Start with the usings from the original info
        var finalUsings = builderInfo.Usings;

        // If there's a matching extended builder, append its namespace and unify with existing usings
        if (extendedBuilderInfos.FirstOrDefault(e => e?.ClassName == builderToExtendName) is { } matchingExtension)
        {
            finalUsings = finalUsings
                .Append($"using {matchingExtension.DeclaredNamespace};")
                .Union(matchingExtension.Usings ?? Array.Empty<string>().ToEquatableArray())
                .ToEquatableArray();
        }

        // Pull out the main elements
        var targetType = builderInfo.TargetTypeName;
        var steps = builderInfo.StepMethods;
        var namespaceName = builderInfo.DeclaredNamespace;
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

        // Build the generated code in a StringBuilder
        var sourceBuilder = new StringBuilder();

        // 1) Write out all the using statements and namespace
        sourceBuilder.Append($$"""
                               {{string.Join("\n", finalUsings ?? new EquatableArray<string>())}}

                               namespace {{namespaceName}};

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
            sourceBuilder.AppendLine($"    public {stepInfo.ParameterType} {fieldName};");
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

        // 8) Optionally include an enum of steps
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

        // 9) If this builder extends another, generate an extension method to jump into the new builder
        if (builderToExtendName is not null)
        {
            var firstStepInfo = stepsArray[0];
            var extensionClassName = $"Init{className}Extensions";
            var extensionInterface =
                $"I{builderToExtendName}{builderInfo.SidePath!.BaseBuilderStep}{genericParams}";
            var extensionReturn = interfaceNames[1];

            sourceBuilder.Append($$"""
                                   public static class {{extensionClassName}}
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

        // 10) Finally, add the generated code to the compilation
        var hintName = $"{namespaceName}.{className}.g.cs";
        context.AddSource(hintName, sourceBuilder.ToString());
    }
}