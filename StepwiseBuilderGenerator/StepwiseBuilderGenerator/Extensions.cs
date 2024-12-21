using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StepwiseBuilderGenerator.DTOs;
using StepwiseBuilderGenerator.HelpersForCache;

namespace StepwiseBuilderGenerator;

public static class Extensions
{
    internal static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> collection) where T : IEquatable<T>?
    {
        return new EquatableArray<T>(collection.ToArray());
    }

    public static TResult? TryCast<TResult>(this object @object) where TResult : class
    {
        return @object as TResult;
    }

    public static IEnumerable<MethodInfo> CollectMethodsInChain(this InvocationExpressionSyntax? invocation)
    {
        var methodsInfo = new List<MethodInfo>();

        if (invocation is null)
        {
            return methodsInfo;
        }

        MemberAccessExpressionSyntax? memberAccessExpression;

        do
        {
            var argumentsList = invocation!.ArgumentList.Arguments;
            
            memberAccessExpression = invocation.Expression as MemberAccessExpressionSyntax;
            invocation = memberAccessExpression!.Expression as InvocationExpressionSyntax;

            var name = memberAccessExpression.Name.Identifier.Text;
            var typeArguments = memberAccessExpression.Name.TryCast<GenericNameSyntax>()?.TypeArgumentList.Arguments
                .Select(static a => a.ToString());

            methodsInfo.Add(new MethodInfo(name, typeArguments, argumentsList));
        } while (memberAccessExpression.Expression is InvocationExpressionSyntax);

        return methodsInfo;
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