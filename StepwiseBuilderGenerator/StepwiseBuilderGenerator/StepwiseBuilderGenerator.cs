using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StepwiseBuilderGenerator.DTOs;
using static System.String;

namespace StepwiseBuilderGenerator;

[Generator]
public class StepwiseBuilderGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var statementsProvider = context.SyntaxProvider.ForAttributeWithMetadataName(typeof(StepwiseBuilder).FullName!,
            static (node, _) =>
            {
                var statements = node
                    .TryCast<ClassDeclarationSyntax>()?
                    .Members.FirstOrDefault(
                        m => m is ConstructorDeclarationSyntax { ParameterList.Parameters.Count: 0 })?
                    .TryCast<ConstructorDeclarationSyntax>()?.Body?.Statements;

                var generateBuilderStatementCall = statements?.FirstOrDefault(s => s
                    .TryFindFirstNode<ObjectCreationExpressionSyntax>()?.Type.TryCast<IdentifierNameSyntax>()
                    ?.Identifier.Text == "GenerateStepwiseBuilder");

                var createBuilderForCallPresence =
                    generateBuilderStatementCall?
                        .TryCast<ExpressionStatementSyntax>()?.Expression
                        .TryCast<InvocationExpressionSyntax>()?.Expression
                        .TryCast<MemberAccessExpressionSyntax>()?
                        .Name.Identifier.Text == "CreateBuilderFor";

                return statements is not { Count: 0 } && generateBuilderStatementCall is not null &&
                       createBuilderForCallPresence;
            },
            static (ctx, _) =>
            {
                var invocation = ctx.TargetNode
                    .TryCast<ClassDeclarationSyntax>()!.Members.First(m => m is ConstructorDeclarationSyntax)
                    .TryCast<ConstructorDeclarationSyntax>()!.Body!.Statements.First(static s => s
                        .TryCast<ExpressionStatementSyntax>()?.Expression
                        .TryCast<InvocationExpressionSyntax>()?.Expression
                        .TryCast<MemberAccessExpressionSyntax>()?.Name.Identifier.Text == "CreateBuilderFor")
                    .TryCast<ExpressionStatementSyntax>()!.Expression
                    .TryCast<InvocationExpressionSyntax>();

                var usings =
                    invocation?.Ancestors()
                        .Select(static a =>
                            a.TryCast<BaseNamespaceDeclarationSyntax>()?.Usings ??
                            a.TryCast<CompilationUnitSyntax>()?.Usings)
                        .Where(static u => u is not null)
                        .SelectMany(static list => list!.Value)
                        .OrderBy(static syntax => syntax.Name.ToString().Length)
                        .Select(static u => u.ToString())
                        .Prepend("using System;")
                        .Distinct()
                        .ToEquatableArray();

                var targetType =
                    invocation?.Expression
                        .TryCast<MemberAccessExpressionSyntax>()!.Name
                        .TryCast<GenericNameSyntax>()!.TypeArgumentList.Arguments[0].ToString();

                var addStepInvocations =
                    invocation
                        .CollectMethodsInChain()
                        .Reverse()
                        .Where(static mi => mi.MethodName == "AddStep")
                        .Select(static (methodInfo, i) => new StepMethodInfo(
                            Order: Int32.Parse(i.ToString()),
                            StepName: methodInfo.ArgumentList!.ToList()[0].Expression
                                .TryCast<LiteralExpressionSyntax>()!.Token.ValueText,
                            FieldName: methodInfo.ArgumentList!.ElementAtOrDefault(1)
                                ?.Expression.TryCast<LiteralExpressionSyntax>()!.Token.ValueText,
                            Type: methodInfo.TypeArguments!.Single()))
                        .OrderBy(static step => step.Order).ToEquatableArray();

                var classDeclaration = GetClassDeclaration(invocation);

                var className = classDeclaration.Identifier.ToString();

                var classTypeParametersAndConstraints =
                    (classDeclaration.TypeParameterList?.Parameters.ToString() ?? "",
                        classDeclaration.ConstraintClauses.ToString());

                var sidePathForBuilders =
                    invocation
                        .CollectMethodsInChain()
                        .Reverse()
                        .Where(static mi => mi.MethodName == "SidePathFrom")
                        .Select(static mi =>
                        {
                            var builderToExtendName =
                                mi.ArgumentList!.ToList()[0]?.Expression.TryCast<LiteralExpressionSyntax>()!.Token
                                    .ValueText;

                            var stepName =
                                mi.ArgumentList!.ToList()[1]?.Expression.TryCast<LiteralExpressionSyntax>()!.Token
                                    .ValueText;

                            return builderToExtendName is not null
                                ? new SidePathInfo(builderToExtendName, stepName!)
                                : null;
                        })
                        .SingleOrDefault();

                var classDeclarationAndStepsOfBuilderToExtend = sidePathForBuilders is null
                    ? new()
                    : (sidePathForBuilders.BuilderToExtendName, addStepInvocations);


                var builderNamespace = GetNamespace(classDeclaration);

                return new BuilderInfo(usings, targetType!, addStepInvocations, className,
                    classTypeParametersAndConstraints, sidePathForBuilders, classDeclarationAndStepsOfBuilderToExtend,
                    builderNamespace);
            }
        );
        
        var stepwiseBuildersInfoForExtension = context.SyntaxProvider.ForAttributeWithMetadataName(typeof(StepwiseBuilder).FullName!,
            static (node, _) =>
            {
                var statements = node
                    .TryCast<ClassDeclarationSyntax>()?
                    .Members.FirstOrDefault(
                        m => m is ConstructorDeclarationSyntax { ParameterList.Parameters.Count: 0 })?
                    .TryCast<ConstructorDeclarationSyntax>()?.Body?.Statements;

                var generateBuilderStatementCall = statements?.FirstOrDefault(s => s
                    .TryFindFirstNode<ObjectCreationExpressionSyntax>()?.Type.TryCast<IdentifierNameSyntax>()
                    ?.Identifier.Text == "GenerateStepwiseBuilder");

                var createBuilderForCallPresence =
                    generateBuilderStatementCall?
                        .TryCast<ExpressionStatementSyntax>()?.Expression
                        .TryCast<InvocationExpressionSyntax>()?.Expression
                        .TryCast<MemberAccessExpressionSyntax>()?
                        .Name.Identifier.Text == "CreateBuilderFor";

                return statements is not { Count: 0 } && generateBuilderStatementCall is not null &&
                       createBuilderForCallPresence;
            },
            static (ctx, _) =>
            {
                var invocation = ctx.TargetNode
                    .TryCast<ClassDeclarationSyntax>()?.Members.FirstOrDefault(m => m is ConstructorDeclarationSyntax)?
                    .TryCast<ConstructorDeclarationSyntax>()?.Body?.Statements.FirstOrDefault(static s => s
                        .TryCast<ExpressionStatementSyntax>()?.Expression
                        .TryCast<InvocationExpressionSyntax>()?.Expression
                        .TryCast<MemberAccessExpressionSyntax>()?.Name.Identifier.Text == "CreateBuilderFor")?
                    .TryCast<ExpressionStatementSyntax>()?.Expression
                    .TryCast<InvocationExpressionSyntax>();

                var classDeclaration = GetClassDeclaration(invocation);

                var builderNamespace = GetNamespace(classDeclaration);

                var classTypeParametersAndConstraints =
                    (classDeclaration.TypeParameterList?.Parameters.ToString() ?? "",
                        classDeclaration.ConstraintClauses.ToString());
                
                var usings =
                    invocation?.Ancestors()
                        .Select(static a =>
                            a.TryCast<BaseNamespaceDeclarationSyntax>()?.Usings ??
                            a.TryCast<CompilationUnitSyntax>()?.Usings)
                        .Where(static u => u is not null)
                        .SelectMany(static list => list!.Value)
                        .OrderBy(static syntax => syntax.Name.ToString().Length)
                        .Select(static u => u.ToString())
                        .Prepend("using System;")
                        .Distinct()
                        .ToEquatableArray();
                
                return new ExtendedBuilderInfo(builderNamespace, classDeclaration.Identifier.ToString(), classTypeParametersAndConstraints, usings);
            }
        );
        
    context.RegisterSourceOutput(statementsProvider.Combine(stepwiseBuildersInfoForExtension.Collect()), GenerateStepwiseBuilders);
    }

    private static ClassDeclarationSyntax? GetClassDeclaration(InvocationExpressionSyntax? invocation)
    {
        if (invocation is null)
        {
            return null;
        }

        var parent = invocation?.Parent;

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

    private static void GenerateStepwiseBuilders(SourceProductionContext context,
        (BuilderInfo Left, ImmutableArray<ExtendedBuilderInfo> Right) tuple)
    {
        var (builderInfo, builderToExtendInfo) = tuple;
        
        var builderToExtendClassName = builderInfo.SidePathInfo?.BuilderToExtendName;
        string? builderToExtendConstraints = null;
        string? builderToExtendGenerics = null;

        var builderUsings = builderInfo.Usings;
        
        if (builderToExtendInfo.FirstOrDefault(info =>
                info?.Name == builderInfo.SidePathInfo?.BuilderToExtendName) is { Generics: not null })
        {
            var info = builderToExtendInfo.First(n =>
                n?.Name == builderInfo.SidePathInfo?.BuilderToExtendName);

            builderUsings = builderUsings.Append($"using {info!.Namespace};").Union(info.Usings ?? []).ToEquatableArray();

            builderToExtendGenerics = info!.Generics.Value.Item1.Split(',').Aggregate(new StringBuilder(), (acc, current) => acc.Append(current + "OriginalGeneric, ")).ToString().TrimEnd().TrimEnd(',');
            builderToExtendConstraints = info!.Generics.Value.Item2;
        }

        var builderTargetType = builderInfo.TargetType;
        var builderAddSteps = builderInfo.AddStepInfos;
        var builderNamespace = builderInfo.Namespace;
        var (builderTypeParameters, builderConstraints) = builderInfo.ClassTypeParametersAndConstraints;
        var builderClassName = builderInfo.ClassName;

        var interfaceNamesToImplement = new List<string>();

        var constraints = builderConstraints is not null ? builderConstraints + "\n" + builderToExtendConstraints : null;
        string? generics = null;

        if (builderTypeParameters is not null && builderTypeParameters is not "")
        {
            generics = builderToExtendGenerics is not null ? "<" + builderTypeParameters + "," + builderToExtendGenerics + ">" : "<" + builderTypeParameters + ">";
        }
        
        else if (builderTypeParameters is null || builderTypeParameters is "")
        {
            generics = "<" + builderToExtendGenerics + ">";
        }

        if (generics == "<>")
        {
            generics = null;
        }

        builderToExtendGenerics = "<" + builderToExtendGenerics + ">";

        var builderClass =
            new StringBuilder();

        builderClass.Append(
            $$"""
              {{Join("\n", builderUsings)}}

              namespace {{builderNamespace}};{{"\n \n"}}
              """);

        for (var s = 0; s < builderAddSteps.Count; s++)
        {
            var step = builderAddSteps.GetArray()![s];
            var nextStepName = s != builderAddSteps.Count - 1 ? builderAddSteps.GetArray()![s + 1].StepName : "Build";
            var interfaceName = $"I{builderClassName}{step.StepName}{generics}";
            interfaceNamesToImplement.Add(interfaceName);

            builderClass.Append(
                $$"""
                  public interface {{interfaceName}} {{constraints}}
                  {
                      I{{builderClassName}}{{nextStepName}}{{generics}} {{step.StepName}}({{step.Type}} value);
                  }


                  """
            );
        }

        var buildInterfaceName = $"I{builderClassName}Build{generics}";
        interfaceNamesToImplement.Add(buildInterfaceName);

        builderClass.Append(
            $$"""

              public interface I{{builderClassName}}Build{{generics}} {{constraints}}
              {
                  {{builderTargetType}} Build(Func<{{builderClassName}}{{generics}}, {{builderTargetType}}> buildFunc);
              }


              """);

        builderClass.Append(
            $$"""
              public partial class {{builderClassName}}{{generics}} : {{Join(",", interfaceNamesToImplement)}} {{constraints}}
              {

              """);

        if (builderToExtendClassName is not null)
        {
            builderClass.Append(
                $$"""
                      public {{builderClassName}}({{builderToExtendClassName}}{{builderToExtendGenerics}} originalBuilder)
                      {
                          OriginalBuilder = originalBuilder;
                      }
                      
                      public {{builderToExtendClassName}}{{builderToExtendGenerics}} OriginalBuilder;


                  """);
        }

        for (var s = 0; s < builderAddSteps.Count; s++)
        {
            var step = builderAddSteps.GetArray()![s];
            var fieldName = step.FieldName ?? step.StepName + "Value";
            var field = $"    public {step.Type} {fieldName};\n";
            builderClass.Append(field);
        }

        builderClass.Append("\n");

        for (var s = 0; s < builderAddSteps.Count; s++)
        {
            var step = builderAddSteps.GetArray()![s];
            var nextStepName = interfaceNamesToImplement[s + 1];
            var fieldName = step.FieldName ?? step.StepName + "Value";

            builderClass.Append(
                $$"""
                      public {{nextStepName}} {{step.StepName}}({{step.Type}} value)
                      {
                          {{fieldName}} = value;
                          return this;
                      }

                  """);
        }

        builderClass.Append(
            $$"""
                  public {{builderTargetType}} Build(Func<{{builderClassName}}{{generics}}, {{builderTargetType}}> buildFunc)
                  {
                      return buildFunc(this);
                  }
              """);

        builderClass.Append(
            $$"""
              
              
                  public enum Steps
                  {

              """);

        foreach (var stepInfo in builderAddSteps)
        {
            builderClass.Append(
                $$"""
                          {{stepInfo.StepName}},

                  """);
        }

        builderClass.Append(
            $$"""
                  }
              }

              """);

        if (builderToExtendClassName is not null)
        {
            builderClass.Append(
                $$"""


                  public static class Init{{builderClassName}}Extensions
                  {
                      public static {{interfaceNamesToImplement[1]}} {{builderAddSteps.GetArray()![0].StepName}}{{generics}}(this I{{builderToExtendClassName}}{{builderInfo.SidePathInfo!.StepToExtendName}}{{builderToExtendGenerics}} originalStep, {{builderAddSteps.GetArray()![0].Type}} value) {{constraints}}
                      {
                          return new {{builderClassName}}{{generics}}(({{builderToExtendClassName}}{{builderToExtendGenerics}}) originalStep).{{builderAddSteps.GetArray()![0].StepName}}(value);
                      }
                  }

                  """);
        }

        context.AddSource($"{builderNamespace + "." + builderClassName + DateTime.Now.Second}.g.cs", builderClass.ToString());
    }
}