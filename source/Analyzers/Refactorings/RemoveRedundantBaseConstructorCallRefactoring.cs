﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslynator.CSharp;

namespace Roslynator.CSharp.Refactorings
{
    internal static class RemoveRedundantBaseConstructorCallRefactoring
    {
        public static void Analyze(SyntaxNodeAnalysisContext context, ConstructorDeclarationSyntax constructor)
        {
            ConstructorInitializerSyntax initializer = constructor.Initializer;

            if (initializer?.Kind() == SyntaxKind.BaseConstructorInitializer
                && initializer.ArgumentList?.Arguments.Count == 0
                && initializer
                    .DescendantTrivia(initializer.Span)
                    .All(f => f.IsWhitespaceOrEndOfLineTrivia()))
            {
                context.ReportDiagnostic(
                    DiagnosticDescriptors.RemoveRedundantBaseConstructorCall,
                    initializer);
            }
        }

        public static Task<Document> RefactorAsync(
            Document document,
            ConstructorDeclarationSyntax constructor,
            CancellationToken cancellationToken)
        {
            ParameterListSyntax parameterList = constructor.ParameterList;
            ConstructorInitializerSyntax initializer = constructor.Initializer;

            if (parameterList.GetTrailingTrivia().IsEmptyOrWhitespace()
                && initializer.GetLeadingTrivia().IsEmptyOrWhitespace())
            {
                ConstructorDeclarationSyntax newConstructor = constructor
                    .WithParameterList(parameterList.WithTrailingTrivia(initializer.GetTrailingTrivia()))
                    .WithInitializer(null);

                return document.ReplaceNodeAsync(constructor, newConstructor, cancellationToken);
            }
            else
            {
                return document.RemoveNodeAsync(initializer, SyntaxRemoveOptions.KeepExteriorTrivia, cancellationToken);
            }
        }
    }
}
