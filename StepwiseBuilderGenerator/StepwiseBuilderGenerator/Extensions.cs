using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StepwiseBuilderGenerator.DTOs;
using StepwiseBuilderGenerator.HelpersForCache;

namespace StepwiseBuilderGenerator;

internal static class Extensions
{
    internal static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> collection) where T : IEquatable<T>?
    {
        return new EquatableArray<T>(collection.ToArray());
    }

    internal static TResult? TryCast<TResult>(this object @object) where TResult : class
    {
        return @object as TResult;
    }

    internal static IEnumerable<MethodInfo> CollectMethodsInChain(this InvocationExpressionSyntax? invocation)
    {
        var methodsInfo = new List<MethodInfo>();

        if (invocation is null)
        {
            return methodsInfo;
        }

        MemberAccessExpressionSyntax? memberAccessExpression;

        do
        {
            var argumentsList = ParseArguments(invocation!.ArgumentList);
            memberAccessExpression = invocation.Expression as MemberAccessExpressionSyntax;
            invocation = memberAccessExpression!.Expression as InvocationExpressionSyntax;

            var name = memberAccessExpression.Name.Identifier.Text;
            var typeArguments = memberAccessExpression.Name.TryCast<GenericNameSyntax>()?.TypeArgumentList.Arguments
                .Select(static a => a.ToString()).ToEquatableArray();

            methodsInfo.Add(new MethodInfo(name, typeArguments, argumentsList));
        } while (memberAccessExpression.Expression is InvocationExpressionSyntax);

        return methodsInfo;
    }

    internal static TResult? TryFindFirstNode<TResult>(this SyntaxNode node, Func<TResult, bool> filter)
        where TResult : class
    {
        // Check if the node is of the type we're looking for
        if (node is TResult result)
        {
            if (filter(result))
            {
                return result; // Stop traversal here if this is the target
            }
        }

        // Recursively traverse each child node
        foreach (var child in node.ChildNodes())
        {
            return TryFindFirstNode(child, filter); // Recursive call for each child node
        }

        return null;
    }

    private static Dictionary<ArgumentType, string?> ParseArguments(ArgumentListSyntax argumentList)
    {
        var parameters = new[]
        {
            ArgumentType.StepName, ArgumentType.FieldName, ArgumentType.DefaultValueFactory,
            ArgumentType.BranchFromStepBeforeStepName, ArgumentType.AndOverloadMapper, ArgumentType.AndOverloadNewName
        };

        return parameters.ToDictionary(parameter => parameter, parameter => GetArgument(argumentList, parameter));
    }

    private static string? GetArgument(ArgumentListSyntax argumentList, ArgumentType type)
    {
        var expr = (argumentList.Arguments.FirstOrDefault(a =>
                        a.NameColon?.Name.Identifier.Text == type.ToArgumentName())
                    ?? (argumentList.Arguments.ElementAtOrDefault(type.ToArgumentOrder())?.NameColon?.Name
                        .Identifier == null
                        ? argumentList.Arguments.ElementAtOrDefault(type.ToArgumentOrder())
                        : null))?.Expression;

        var literalExpr = expr?.TryCast<LiteralExpressionSyntax>()?.ToString().Trim('"');

        return literalExpr ?? expr?.ToString();
    }
}