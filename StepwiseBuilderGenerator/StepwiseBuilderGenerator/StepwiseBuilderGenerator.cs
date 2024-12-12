using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static System.String;

namespace StepwiseBuilderGenerator;

[Generator]
public class StepwiseBuilderGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var sw = Stopwatch.StartNew();

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
                
                return statements is not { Count: 0 } && generateBuilderStatementCall is not null && createBuilderForCallPresence is true;
            },
            static (ctx, _) =>
                ctx.TargetNode
                    .TryCast<ClassDeclarationSyntax>()!.Members.First(m => m is ConstructorDeclarationSyntax)
                    .TryCast<ConstructorDeclarationSyntax>()!.Body!.Statements.First(static s => s
                        .TryCast<ExpressionStatementSyntax>()?.Expression
                        .TryCast<InvocationExpressionSyntax>()?.Expression
                        .TryCast<MemberAccessExpressionSyntax>()?.Name.Identifier.Text == "CreateBuilderFor")
                    .TryCast<ExpressionStatementSyntax>()!.Expression
                    .TryCast<InvocationExpressionSyntax>()
        );

        var usings = statementsProvider.Select(static (invocation, _) => invocation?.Ancestors()
            .Select(static a =>
                a.TryCast<BaseNamespaceDeclarationSyntax>()?.Usings ?? a.TryCast<CompilationUnitSyntax>()?.Usings)
            .Where(static u => u is not null)
            .SelectMany(static list => list!.Value)
            .OrderBy(static syntax => syntax.Name.ToString().Length)
            .Select(static u => u.ToString())
            .Prepend("using System;")
            .Distinct()
            .ToImmutableHashSet());

        var targetType = statementsProvider.Select(static (invocation, _) =>
            invocation?.Expression
                .TryCast<MemberAccessExpressionSyntax>()!.Name
                .TryCast<GenericNameSyntax>()!.TypeArgumentList.Arguments[0].ToString());

        var addStepInvocations = statementsProvider.Select(static (invocation, _) =>
            invocation
                .CollectMethodsInChain()
                .Reverse()
                .Where(static mi => mi.MethodName == "AddStep")
                .Select(static (methodInfo, i) => new StepMethod(
                    Order: Int32.Parse(i.ToString()),
                    StepName: methodInfo.ArgumentList!.ToList()[0].Expression
                        .TryCast<LiteralExpressionSyntax>()!.Token.ValueText,
                    FieldName: methodInfo.ArgumentList!.ElementAtOrDefault(1)
                        ?.Expression.TryCast<LiteralExpressionSyntax>()!.Token.ValueText,
                    Type: methodInfo.TypeArguments!.Single()))
                .OrderBy(static step => step.Order)
        );

        var classDeclaration = statementsProvider.Select(static (invocation, _) =>
        {
            var parent = invocation!.Parent;

            while (parent is not ClassDeclarationSyntax)
            {
                parent = parent!.Parent;
            }

            return (ClassDeclarationSyntax)parent;
        });

        var collectedClassDeclarations = classDeclaration.Collect();

        var @namespace = context.CompilationProvider
            .Combine(collectedClassDeclarations)
            .Select(static (compilationWithClassDeclaration, _) =>
            {
                var (compilation, classDeclaration) = compilationWithClassDeclaration;
                return classDeclaration.Select(c =>
                    compilation.GetSemanticModel(c.SyntaxTree).GetDeclaredSymbol(c)).Select(static nt =>
                    Join(".", nt!.ContainingNamespace.ConstituentNamespaces)).ToList();
            });

        var className =
            classDeclaration.Select(static (c, _) => c.Identifier.ToString());

        var classTypeParametersAndConstraints =
            classDeclaration.Select(static (c, _) => (
                c.TypeParameterList?.ToString() ?? "",
                c.ConstraintClauses.ToString()));

        // All info that is needed to build main builder
        var usingsCollected = usings.Collect();
        var targetTypesCollected = targetType.Collect();
        var addStepInvocationsCollected = addStepInvocations.Collect();
        var classTypeParametersAndConstraintsCollected = classTypeParametersAndConstraints.Collect();
        var classNamesCollected = className.Collect();

        var sidePathForBuilders = statementsProvider.Select(static (invocation, _) =>
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

                    return builderToExtendName is not null ? new SidePathInfo(builderToExtendName, stepName!) : null;
                })
                .SingleOrDefault());

        var classDeclarationAndStepsOfBuilderToExtend =
            sidePathForBuilders
                .Combine(collectedClassDeclarations.Combine(addStepInvocationsCollected))
                .Select(static (provider, _) =>
                {
                    var indexOf = provider.Right.Left.ToList()
                        .FindIndex(c => c.Identifier.Text == provider.Left?.BuilderToExtendName);

                    if (indexOf != -1)
                    {
                        return (
                            provider.Right.Left.SingleOrDefault(
                                c => c.Identifier.Text == provider.Left?.BuilderToExtendName),
                            provider.Right.Right[indexOf]);
                    }

                    return new();
                });

        var buildersToExtendClasses =
            classDeclarationAndStepsOfBuilderToExtend.Select(static (c, _) => c.Item1).Collect();

        var buildersToExtendClassesNames =
            buildersToExtendClasses.Select(static (c, _) =>
                c.Select(c => (c?.Identifier.Text.ToString(), c?.TypeParameterList?.Parameters.ToString())).ToList());

        var namespaceOfBuilderToExtend = context.CompilationProvider
            .Combine(buildersToExtendClasses).Select(static (compilationWithClassDeclaration, _) =>
            {
                var (compilation, classDeclaration) = compilationWithClassDeclaration;
                return classDeclaration.Select(c =>
                        c is not null ? compilation.GetSemanticModel(c.SyntaxTree).GetDeclaredSymbol(c) : null)
                    .Select(static nt =>
                        nt is not null ? Join(".", nt.ContainingNamespace.ConstituentNamespaces) : null).ToList();
            });

        var stepsOfBuilderToExtend = classDeclarationAndStepsOfBuilderToExtend.Select(static (c, _) => c.Item2);

        var generateBuildersComplexCompilation =
            usingsCollected
                .Combine(targetTypesCollected)
                .Combine(addStepInvocationsCollected)
                .Combine(@namespace)
                .Combine(classTypeParametersAndConstraintsCollected)
                .Combine(classNamesCollected)
                .Combine(sidePathForBuilders.Collect())
                .Combine(stepsOfBuilderToExtend.Collect())
                .Combine(namespaceOfBuilderToExtend)
                .Combine(buildersToExtendClassesNames);

        sw.Stop();
        Console.WriteLine($"Generation data collected in: {sw.Elapsed}");

        context.RegisterSourceOutput(generateBuildersComplexCompilation, ComplexGenerateStepwiseBuilders);
    }

    private void ComplexGenerateStepwiseBuilders(SourceProductionContext context,
        (((((((((ImmutableArray<ImmutableHashSet<string>?> Left, ImmutableArray<string?> Right) Left,
            ImmutableArray<IOrderedEnumerable<StepMethod>> Right) Left, List<string> Right) Left,
            ImmutableArray<(string, string)> Right) Left, ImmutableArray<string> Right) Left,
            ImmutableArray<SidePathInfo?> Right) Left, ImmutableArray<IOrderedEnumerable<StepMethod>> Right) Left,
            List<string?> Right) Left, List<(string?, string?)> Right) valueTuple)
    {
        var sw = Stopwatch.StartNew();
        var (((((((((usings, targetType), addStepsInvocations), namespaces), constraintClauses), className), sidePaths),
            builderToExtendSteps), namespaceOfBuilderToExtend), buildersToExtendClassesNames) = valueTuple;

        for (var i = 0; i < usings.Length; i++)
        {
            var sidePathName = sidePaths.ElementAtOrDefault(i);
            var stepsToExtend = builderToExtendSteps.ElementAtOrDefault(i)?.ToList();
            var extendedStepIndex =
                stepsToExtend?.ToList().FindIndex(s => s.StepName == sidePathName!.StepToExtendName) + 1;
            string? extendedStep = null;

            if (extendedStepIndex.HasValue && extendedStepIndex != stepsToExtend!.Count)
            {
                extendedStep = stepsToExtend[extendedStepIndex.Value].StepName;
            }

            else if (extendedStepIndex.HasValue && extendedStepIndex == stepsToExtend!.Count)
            {
                extendedStep = "Build";
            }

            var namespaceOfOriginalToBuilder = namespaceOfBuilderToExtend.ElementAtOrDefault(i);
            var builderToExtendClassName = buildersToExtendClassesNames.ElementAtOrDefault(i).Item1;
            var builderToExtendGenerics = buildersToExtendClassesNames.ElementAtOrDefault(i).Item2 is not null
                ? $"<{buildersToExtendClassesNames.ElementAtOrDefault(i).Item2}>"
                : "";

            var builderUsings = usings[i]
                .Append(namespaceOfOriginalToBuilder is not null
                    ? $"using {namespaceOfOriginalToBuilder};"
                    : "");

            var builderTargetType = targetType[i];
            var builderAddSteps = addStepsInvocations[i].ToList();
            var builderNamespace = namespaces[i];
            var (builderTypeParameters, builderConstraints) = constraintClauses[i];
            var builderClassName = className[i];

            var interfaceNamesToImplement = new List<string>();

            var constraints = builderConstraints;
            var generics = builderTypeParameters;

            var builderClass =
                new StringBuilder();

            builderClass.Append(
                $$"""
                  {{Join("\n", builderUsings)}}

                  namespace {{builderNamespace}};{{"\n \n"}}
                  """);

            for (var s = 0; s < builderAddSteps.Count; s++)
            {
                var step = builderAddSteps[s];
                var nextStepName = s != builderAddSteps.Count - 1 ? builderAddSteps[s + 1].StepName : "Build";
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
                var step = builderAddSteps[s];
                var fieldName = step.FieldName ?? step.StepName + "Value";
                var field = $"    public {step.Type} {fieldName};\n";
                builderClass.Append(field);
            }

            builderClass.Append("\n");

            for (var s = 0; s < builderAddSteps.Count; s++)
            {
                var step = builderAddSteps[s];
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
                          public static {{interfaceNamesToImplement[1]}} {{builderAddSteps[0].StepName}}{{generics}}(this I{{builderToExtendClassName}}{{extendedStep}}{{builderToExtendGenerics}} originalStep, {{builderAddSteps[0].Type}} value) {{constraints}}
                          {
                              return new {{builderClassName}}{{generics}}(({{builderToExtendClassName}}{{builderToExtendGenerics}}) originalStep).{{builderAddSteps[0].StepName}}(value);
                          }
                      }

                      """);
            }

            sw.Stop();
            Console.WriteLine($"Builder generated in {sw.Elapsed}");

            sw.Restart();
            context.AddSource($"{builderClassName}.g.cs", builderClass.ToString());

            sw.Stop();
            Console.WriteLine($"Builder file generated in {sw.Elapsed}");
        }
    }
}