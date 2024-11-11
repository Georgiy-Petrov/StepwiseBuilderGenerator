using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static System.String;

namespace StepwiseBuilderGenerator;

[Generator]
public class StepwiseBuilderSidePathGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        Console.WriteLine("InitStarted: " + DateTime.Now);

        var statementsProvider = context.SyntaxProvider.ForAttributeWithMetadataName(typeof(StepwiseBuilder).FullName!,
            static (node, _) =>
            {
                var statements = node
                    .TryCast<ClassDeclarationSyntax>()?
                    .Members.FirstOrDefault(
                        m => m is ConstructorDeclarationSyntax { ParameterList.Parameters.Count: 0 })?
                    .TryCast<ConstructorDeclarationSyntax>()?.Body?.Statements;

                var generateBuilderStatementPresence = statements?.SingleOrDefault(s => s
                    .TryFindFirstNode<ObjectCreationExpressionSyntax>()?.Type.TryCast<IdentifierNameSyntax>()
                    ?.Identifier.Text == "GenerateStepwiseBuilder");

                return statements is not { Count: 0 } && generateBuilderStatementPresence is not null;
            },
            static (ctx, _) =>
                ctx.TargetNode
                    .TryCast<ClassDeclarationSyntax>()!.Members.First(m => m is ConstructorDeclarationSyntax)
                    .TryCast<ConstructorDeclarationSyntax>()!.Body!.Statements
        );

        var originalBuildersProvider = statementsProvider.Select(
            static (sl, _) => sl
                .Where(static s =>
                    s.TryFindFirstNode<ObjectCreationExpressionSyntax>()
                        ?.Type.TryCast<IdentifierNameSyntax>()
                        ?.Identifier.Text == "GenerateStepwiseBuilder")
                .Single(static s => s
                    .TryCast<ExpressionStatementSyntax>()?.Expression
                    .TryCast<InvocationExpressionSyntax>()?.Expression
                    .TryCast<MemberAccessExpressionSyntax>()?.Name.Identifier.Text == "CreateBuilderFor")
                .TryCast<ExpressionStatementSyntax>()!.Expression
                .TryCast<InvocationExpressionSyntax>());

        var usings = originalBuildersProvider.Select(static (invocation, _) => invocation?.Ancestors()
            .Select(static a =>
                a.TryCast<BaseNamespaceDeclarationSyntax>()?.Usings ?? a.TryCast<CompilationUnitSyntax>()?.Usings)
            .Where(static u => u is not null)
            .SelectMany(static list => list!.Value)
            .OrderBy(static syntax => syntax.Name.ToFullString().Length)
            .Select(static u => u.ToString())
            .Prepend("using System;")
            .Distinct()
            .ToImmutableHashSet());

        var targetType = originalBuildersProvider.Select(static (invocation, _) =>
            invocation?.Expression
                .TryCast<MemberAccessExpressionSyntax>()!.Name
                .TryCast<GenericNameSyntax>()!.TypeArgumentList.Arguments[0].ToString());

        var addStepInvocations = originalBuildersProvider.Select(static (invocation, _) =>
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

        var classDeclaration = originalBuildersProvider.Select(static (invocation, _) =>
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
                c.TypeParameterList?.Parameters.Select(p => p.ToString()).ToList() ?? new List<string>(),
                c.ConstraintClauses.Select(c => c.ToString()).ToImmutableHashSet()));

        var usingsCollected = usings.Collect();
        var targetTypesCollected = targetType.Collect();
        var addStepInvocationsCollected = addStepInvocations.Collect();
        var classTypeParametersAndConstraintsCollected = classTypeParametersAndConstraints.Collect();
        var classNamesCollected = className.Collect();

        var sidePathForBuilders = originalBuildersProvider.Select(static (invocation, _) =>
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
            buildersToExtendClasses.Select(static (c, _) => c.Select(c => c?.Identifier.Text.ToString()).ToList());

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

        Console.WriteLine("InitFinished: " + DateTime.Now);

        context.RegisterSourceOutput(generateBuildersComplexCompilation, ComplexGenerateStepwiseBuilders);
    }

    private void ComplexGenerateStepwiseBuilders(SourceProductionContext context,
        (((((((((ImmutableArray<ImmutableHashSet<string>> Left, ImmutableArray<string> Right) Left,
            ImmutableArray<IOrderedEnumerable<StepMethod>> Right) Left, List<string> Right) Left,
            ImmutableArray<(List<string>, ImmutableHashSet<string>)> Right) Left, ImmutableArray<string> Right) Left,
            ImmutableArray<SidePathInfo> Right) Left, ImmutableArray<IOrderedEnumerable<StepMethod>> Right) Left,
            List<string> Right) Left, List<string> Right) valueTuple)
    {
        Console.WriteLine("BuilderStarted: " + DateTime.Now);
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
            var builderToExtendClassName = buildersToExtendClassesNames.ElementAtOrDefault(i);

            var builderUsings = usings[i].Append(namespaceOfOriginalToBuilder is not null
                ? $"using {namespaceOfOriginalToBuilder};"
                : "");
            var builderTargetType = targetType[i];
            var builderAddSteps = addStepsInvocations[i].ToList();
            var builderNamespace = namespaces[i];
            var (builderTypeParameters, builderConstraints) = constraintClauses[i];
            var builderClassName = className[i];


            var interfaceNamesToImplement = new List<string>();

            var constraints = builderConstraints.Count > 0 ? Join("\n", builderConstraints) : Empty;
            var generics = builderTypeParameters.Count > 0 ? $"<{Join(",", builderTypeParameters)}>" : Empty;

            StringBuilder AddUsingsAndNamespace(StringBuilder sb)
            {
                return sb.Append(
                    $$"""
                      {{Join("\n", builderUsings)}}

                      namespace {{@builderNamespace}};{{"\n \n"}}
                      """);
            }

            StringBuilder AddConstructor(StringBuilder sb)
            {
                if (builderToExtendClassName is null)
                {
                    return sb;
                }

                return sb.Append(
                    $$"""
                          public {{builderClassName}}({{builderToExtendClassName}}{{generics}} originalBuilder)
                          {
                              OriginalBuilder = originalBuilder;
                          }
                          
                          public {{builderToExtendClassName}}{{generics}} OriginalBuilder;


                      """);
            }

            var builderClass =
                new StringBuilder();

            AddUsingsAndNamespace(builderClass);

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

            AddConstructor(builderClass);

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
                  }
                  """);

            StringBuilder AddFactoryExtensionClass(StringBuilder sb)
            {
                if (builderToExtendClassName is null)
                {
                    return sb;
                }

                return sb.Append(
                    $$"""


                      public static class Init{{builderClassName}}Extensions
                      {
                          public static {{interfaceNamesToImplement[1]}} {{builderAddSteps[0].StepName}}{{generics}}(this I{{builderToExtendClassName}}{{extendedStep}}{{generics}} originalStep, {{builderAddSteps[0].Type}} value)
                          {
                              return new {{builderClassName}}{{generics}}(({{builderToExtendClassName}}{{generics}}) originalStep).{{builderAddSteps[0].StepName}}(value);
                          }
                      }

                      """);
            }


            AddFactoryExtensionClass(builderClass);

            Console.WriteLine("BuilderFinished: " + DateTime.Now);
            context.AddSource($"{builderClassName}.g.cs", builderClass.ToString());
        }
    }
}