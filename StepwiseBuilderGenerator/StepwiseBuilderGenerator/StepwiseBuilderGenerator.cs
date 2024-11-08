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

public static class Extensions
{
    public static TResult? TryCast<TResult>(this object @object) where TResult : class
    {
        return @object as TResult;
    }

    public static TResult? TryFindFirstNode<TResult>(this SyntaxNode node) where TResult : class
    {
        // Check if the node is of the type we're looking for
        if (node is TResult result)
        {
            return result; // Stop traversal here if this is the target
        }

        // Recursively traverse each child node
        foreach (var child in node.ChildNodes())
        {
            return TryFindFirstNode<TResult>(child); // Recursive call for each child node
        }

        return null;
    }
}

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
            (sl, _) => sl
                .Where(s => s.TryFindFirstNode<ObjectCreationExpressionSyntax>()?.Type.TryCast<IdentifierNameSyntax>()
                    ?.Identifier.Text == "GenerateStepwiseBuilder").Single(s => s
                    .TryCast<ExpressionStatementSyntax>()?.Expression
                    .TryCast<InvocationExpressionSyntax>()?.Expression
                    .TryCast<MemberAccessExpressionSyntax>()?.Name.Identifier.Text == "CreateBuilderFor")
                .TryCast<ExpressionStatementSyntax>()!.Expression
                .TryCast<InvocationExpressionSyntax>());

        var usings = originalBuildersProvider.Select(static (invocation, _) => invocation?.Ancestors()
            .Select(a =>
                a.TryCast<BaseNamespaceDeclarationSyntax>()?.Usings ?? a.TryCast<CompilationUnitSyntax>()?.Usings)
            .Where(u => u is not null)
            .SelectMany(list => list!.Value)
            .OrderBy(syntax => syntax.Name.ToFullString().Length)
            .Select(u => u.ToString())
            .Append("using System;")
            .Distinct()
            .ToImmutableHashSet());

        var targetType = originalBuildersProvider.Select(static (invocation, _) =>
            invocation?.Expression
                .TryCast<MemberAccessExpressionSyntax>()!.Name
                .TryCast<GenericNameSyntax>()!.TypeArgumentList.Arguments[0].ToString());

        var addStepInvocations = originalBuildersProvider.Select(static (invocation, _) =>
            {
                var addStepInvocations =
                    new LinkedList<(int Index, InvocationExpressionSyntax InvocationExpressionSyntax)>();

                var expression = invocation!.Expression as MemberAccessExpressionSyntax;

                while (expression!.Expression is InvocationExpressionSyntax nextExpression)
                {
                    addStepInvocations.AddLast((0, nextExpression));
                    expression = nextExpression.Expression as MemberAccessExpressionSyntax;
                }
                
                var invocations = addStepInvocations.Reverse().ToArray();

                for (var i = 0; i < invocations.Length; i++)
                {
                    invocations[i].Index = i;
                }

                return invocations;
            })
            .Select((stepsInvocations, _) =>
                stepsInvocations.Select(invocation =>
                    new StepMethod(
                        Order: Int32.Parse(invocation.Index.ToString()),
                        StepName: invocation.InvocationExpressionSyntax.ArgumentList.Arguments[0].Expression
                            .TryCast<LiteralExpressionSyntax>()!.Token.ValueText,
                        FieldName: invocation.InvocationExpressionSyntax.ArgumentList.Arguments.ElementAtOrDefault(1)
                            ?.Expression.TryCast<LiteralExpressionSyntax>()!.Token.ValueText,
                        Type: invocation.InvocationExpressionSyntax.Expression.TryCast<MemberAccessExpressionSyntax>()!
                            .Name.TryCast<GenericNameSyntax>()!.TypeArgumentList.Arguments[0].ToString())
                ).OrderBy(step => step.Order)
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

        var @namespace = context.CompilationProvider
            .Combine(classDeclaration.Collect())
            .Select((compilationWithClassDeclaration, _) =>
            {
                var (compilation, classDeclaration) = compilationWithClassDeclaration;
                return classDeclaration.Select(c =>
                    compilation.GetSemanticModel(c.SyntaxTree).GetDeclaredSymbol(c)).Select(nt =>
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


        var generateBuildersCompilation =
            usingsCollected
                .Combine(targetTypesCollected)
                .Combine(addStepInvocationsCollected)
                .Combine(@namespace)
                .Combine(classTypeParametersAndConstraintsCollected)
                .Combine(classNamesCollected);
        
        context.RegisterSourceOutput(generateBuildersCompilation, GenerateStepwiseBuilders);
        
        var sidePathsProvider = statementsProvider.Select(
            (sl, _) => sl.Where(s => s.TryFindFirstNode<ObjectCreationExpressionSyntax>()
                    ?.Type.TryCast<IdentifierNameSyntax>()
                    ?.Identifier.Text == "GenerateSidePathForStepwiseBuilder").Where(s => s
                    .TryCast<ExpressionStatementSyntax>()?.Expression
                    .TryCast<InvocationExpressionSyntax>()?.Expression
                    .TryCast<MemberAccessExpressionSyntax>()?.Name.Identifier.Text == "CreateBuilderFor")
                .Select(s =>
                    s.TryCast<ExpressionStatementSyntax>()!.Expression.TryCast<InvocationExpressionSyntax>())
                .ToImmutableArray()
        );
        
        var sidePathsForBuilders = sidePathsProvider.Select((invocations, _) => invocations.Select(invocation =>
        {
            var expression = invocation!.Expression as MemberAccessExpressionSyntax;

            while (expression!.Expression is InvocationExpressionSyntax nextExpression)
            {
                if (nextExpression.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "SidePathFrom" })
                {
                    return new SidePathInfo(
                        StepToExtendName: nextExpression.ArgumentList.Arguments[0].Expression
                            .TryCast<LiteralExpressionSyntax>()!.Token.ValueText);
                }

                expression = nextExpression.Expression as MemberAccessExpressionSyntax;
            }

            return null;
        }).ToImmutableArray());
        
        var addStepInvocationsForSidePaths = sidePathsProvider.Select(static (methodsInvocations, _) =>
            {
                var addStepInvocationsForAllSidePaths = new List<List<(int Index, InvocationExpressionSyntax InvocationExpressionSyntax)>> {};
                
                foreach (var invocation in methodsInvocations)
                {
                    var addStepInvocations =
                        new LinkedList<(int Index, InvocationExpressionSyntax InvocationExpressionSyntax)>();

                    var expression = invocation!.Expression as MemberAccessExpressionSyntax;

                    while (expression!.Expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "AddStep" } syntax } nextExpression)
                    {
                        addStepInvocations.AddLast((0, nextExpression));
                        expression = syntax;
                    }
                
                    var reversedInvocations = addStepInvocations.Reverse().ToArray();

                    for (var i = 0; i < reversedInvocations.Length; i++)
                    {
                        reversedInvocations[i].Index = i;
                    }

                    addStepInvocationsForAllSidePaths.Add(reversedInvocations.ToList());
                }
                
                return addStepInvocationsForAllSidePaths;
            })
            .Select((stepsInvocations, _) =>
                stepsInvocations.Select(invocations =>
                    invocations.Select(invocation =>
                    new StepMethod(
                        Order: Int32.Parse(invocation.Index.ToString()),
                        StepName: invocation.InvocationExpressionSyntax.ArgumentList.Arguments[0].Expression
                            .TryCast<LiteralExpressionSyntax>()!.Token.ValueText,
                        FieldName: invocation.InvocationExpressionSyntax.ArgumentList.Arguments.ElementAtOrDefault(1)
                            ?.Expression.TryCast<LiteralExpressionSyntax>()!.Token.ValueText,
                        Type: invocation.InvocationExpressionSyntax.Expression.TryCast<MemberAccessExpressionSyntax>()!
                            .Name.TryCast<GenericNameSyntax>()!.TypeArgumentList.Arguments[0].ToString())
                ).OrderBy(step => step.Order)).ToImmutableArray()
            );

        var sidePathsForBuildersCollected = sidePathsForBuilders.Collect();
        var addStepInvocationsForSidePathsCollected = addStepInvocationsForSidePaths.Collect();
        
        var generateBuildersSidePathsCompilation =
            addStepInvocationsCollected
                .Combine(sidePathsForBuildersCollected)
                .Combine(usingsCollected
                .Combine(targetTypesCollected)
                .Combine(addStepInvocationsForSidePathsCollected)
                .Combine(@namespace)
                .Combine(classTypeParametersAndConstraintsCollected)
                .Combine(classNamesCollected));

        Console.WriteLine("InitFinished: " + DateTime.Now);
        
        context.RegisterSourceOutput(generateBuildersSidePathsCompilation, GenerateSidePathsForStepwiseBuilders);
    }

    private void GenerateStepwiseBuilders(SourceProductionContext context,
        (((((ImmutableArray<ImmutableHashSet<string>?> Left, ImmutableArray<string?> Right) Left,
            ImmutableArray<IOrderedEnumerable<StepMethod>> Right) Left, List<string> Right) Left,
            ImmutableArray<(List<string>, ImmutableHashSet<string>)> Right) Left, ImmutableArray<string> Right)
            providers)
    {
        Console.WriteLine("BuilderStarted: " + DateTime.Now);
        var (((((usings, targetType), addStepsInvocations), namespaces), constraintClauses), className) = providers;

        for (var i = 0; i < usings.Length; i++)
        {
            var builderUsings = usings[i];
            var builderTargetType = targetType[i];
            var builderAddSteps = addStepsInvocations[i].ToList();
            var builderNamespace = namespaces[i];
            var (builderTypeParameters, builderConstraints) = constraintClauses[i];
            var builderClassName = className[i];

            var builderClass =
                new StringBuilder(
                    $$"""
                      {{Join("\n", builderUsings)}}

                      namespace {{builderNamespace}};{{"\n \n"}}
                      """);

            var constraints = builderConstraints.Count > 0 ? Join("\n", builderConstraints) : Empty;
            var generics = builderTypeParameters.Count > 0 ? $"<{Join(",", builderTypeParameters)}>" : Empty;
            var interfaceNamesToImplement = new List<string>();

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
                  }{{"\n \n"}}
                  """);

            builderClass.Append(
                $$"""
                  public partial class {{builderClassName}}{{generics}} : {{Join(",", interfaceNamesToImplement)}} {{constraints}}
                  {

                  """);

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

            Console.WriteLine("BuilderFinished: " + DateTime.Now);
            context.AddSource($"{builderClassName}.g.cs", builderClass.ToString());
        }
    }

    private void GenerateSidePathsForStepwiseBuilders(SourceProductionContext context,
        ((ImmutableArray<IOrderedEnumerable<StepMethod>> Left, ImmutableArray<ImmutableArray<SidePathInfo>> Right) Left, (((((ImmutableArray<ImmutableHashSet<string>> Left, ImmutableArray<string> Right) Left, ImmutableArray<ImmutableArray<IOrderedEnumerable<StepMethod>>> Right) Left, List<string> Right) Left, ImmutableArray<(List<string>, ImmutableHashSet<string>)> Right) Left, ImmutableArray<string> Right) Right) providers)
    {
        Console.WriteLine("ExtensionsStarted: " + DateTime.Now);
        var ((originalBuilderAddStepInvocations, sidePaths), (((((usings, targetType), addStepsInvocations), namespaces), constraintClauses), className)) =
            providers;

        if (addStepsInvocations.Length == 0)
        {
            return;
        }

        for (var i = 0; i < sidePaths.Length; i++)
        {
            for (var j = 0; j < sidePaths[i].Length; j++)
            {
                var builderUsings = usings[i];
                var builderTargetType = targetType[i];
                var builderAddSteps = addStepsInvocations[i][j].ToList();
                var builderNamespace = namespaces[i];
                var (builderTypeParameters, builderConstraints) = constraintClauses[i];
                var originalBuilderClassName = className[i];
                var builderClassName = className[i] + $"{builderAddSteps[0].StepName}SidePath";
                var stepToExtendIndex = originalBuilderAddStepInvocations[i].Select(si => si.StepName).ToList().IndexOf(sidePaths[i][j].StepToExtendName) + 1;
                var stepToExtend = stepToExtendIndex < originalBuilderAddStepInvocations[i].Count() ? originalBuilderAddStepInvocations[i].ToList()[stepToExtendIndex].StepName : "Build";

                var builderClass =
                    new StringBuilder(
                        $$"""
                          {{Join("\n", builderUsings)}}

                          namespace {{builderNamespace}};{{"\n \n"}}
                          """);

                var constraints = builderConstraints.Count > 0 ? Join("\n", builderConstraints) : Empty;
                var generics = builderTypeParameters.Count > 0 ? $"<{Join(",", builderTypeParameters)}>" : Empty;
                var interfaceNamesToImplement = new List<string>();

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
                      }{{"\n \n"}}
                      """);

                builderClass.Append(
                    $$"""
                      public class {{builderClassName}}{{generics}} : {{Join(",", interfaceNamesToImplement)}} {{constraints}}
                      {

                      """);

                builderClass.Append(
                    $$"""
                          public {{builderClassName}}({{originalBuilderClassName}}{{generics}} originalBuilder)
                          {
                              OriginalBuilder = originalBuilder;
                          }
                          
                      
                      """);

                builderClass.Append(
                    $$"""
                          public {{originalBuilderClassName}}{{generics}} OriginalBuilder;

                      """);

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

                builderClass.Append(
                    $$"""
                      public static class Init{{builderClassName}}Extensions
                      {
                          public static {{interfaceNamesToImplement[1]}} {{builderAddSteps[0].StepName}}{{generics}}(this I{{originalBuilderClassName}}{{stepToExtend}}{{generics}} originalStep, {{builderAddSteps[0].Type}} value)
                          {
                              return new {{builderClassName}}{{generics}}(({{originalBuilderClassName}}{{generics}}) originalStep).{{builderAddSteps[0].StepName}}(value);
                          }
                      }

                      """);


                Console.WriteLine("ExtensionsFinished: " + DateTime.Now);
                context.AddSource($"{builderClassName}.g.cs", builderClass.ToString());
            }
        }
    }
}

record SidePathInfo(string StepToExtendName)
{
    public string StepToExtendName { get; } = StepToExtendName;
}

record StepMethod(int Order, string StepName, string? FieldName, string Type)
{
    public int Order { get; } = Order;
    public string StepName { get; } = StepName;
    public string? FieldName { get; } = FieldName;
    public string Type { get; } = Type;
}