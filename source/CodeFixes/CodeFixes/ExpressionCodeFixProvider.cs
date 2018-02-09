﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslynator.CodeFixes;
using Roslynator.CSharp.Refactorings;
using Roslynator.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Roslynator.CSharp.CSharpFactory;

namespace Roslynator.CSharp.CodeFixes
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ExpressionCodeFixProvider))]
    [Shared]
    public class ExpressionCodeFixProvider : BaseCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(
                    CompilerDiagnosticIdentifiers.CannotImplicitlyConvertTypeExplicitConversionExists,
                    CompilerDiagnosticIdentifiers.ConstantValueCannotBeConverted,
                    CompilerDiagnosticIdentifiers.ExpressionBeingAssignedMustBeConstant,
                    CompilerDiagnosticIdentifiers.CannotConvertNullToTypeBecauseItIsNonNullableValueType,
                    CompilerDiagnosticIdentifiers.ResultOfExpressionIsAlwaysConstantSinceValueIsNeverEqualToNull,
                    CompilerDiagnosticIdentifiers.CannotConvertNullToTypeParameterBecauseItCouldBeNonNullableValueType,
                    CompilerDiagnosticIdentifiers.OnlyAssignmentCallIncrementDecrementAndNewObjectExpressionsCanBeUsedAsStatement,
                    CompilerDiagnosticIdentifiers.CannotImplicitlyConvertType);
            }
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            if (!Settings.IsCodeFixEnabled(CodeFixIdentifiers.AddComparisonWithBooleanLiteral)
                && !Settings.IsCodeFixEnabled(CodeFixIdentifiers.CreateSingletonArray)
                && !Settings.IsCodeFixEnabled(CodeFixIdentifiers.UseUncheckedExpression)
                && !Settings.IsCodeFixEnabled(CodeFixIdentifiers.RemoveConstModifier)
                && !Settings.IsCodeFixEnabled(CodeFixIdentifiers.ReplaceNullLiteralExpressionWithDefaultValue)
                && !Settings.IsCodeFixEnabled(CodeFixIdentifiers.UseCoalesceExpression)
                && !Settings.IsCodeFixEnabled(CodeFixIdentifiers.RemoveConditionThatIsAlwaysEqualToTrueOrFalse)
                && !Settings.IsCodeFixEnabled(CodeFixIdentifiers.IntroduceLocalVariable)
                && !Settings.IsCodeFixEnabled(CodeFixIdentifiers.IntroduceField)
                && !Settings.IsCodeFixEnabled(CodeFixIdentifiers.ReplaceStringLiteralWithCharacterLiteral)
                && !Settings.IsCodeFixEnabled(CodeFixIdentifiers.UseYieldReturnInsteadOfReturn)
                && !Settings.IsCodeFixEnabled(CodeFixIdentifiers.ChangeMemberTypeAccordingToReturnExpression)
                && !Settings.IsCodeFixEnabled(CodeFixIdentifiers.AddArgumentList)
                && !Settings.IsCodeFixEnabled(CodeFixIdentifiers.ReplaceConditionalExpressionWithIfElse)
                && !Settings.IsCodeFixEnabled(CodeFixIdentifiers.ReplaceConstantWithField)
                && !Settings.IsCodeFixEnabled(CodeFixIdentifiers.ChangeTypeAccordingToInitializer)
                && !Settings.IsCodeFixEnabled(CodeFixIdentifiers.ReplaceYieldReturnWithForEach)
                && !Settings.IsCodeFixEnabled(CodeFixIdentifiers.ReplaceComparisonWithAssignment))
            {
                return;
            }

            SyntaxNode root = await context.GetSyntaxRootAsync().ConfigureAwait(false);

            if (!TryFindNode(root, context.Span, out ExpressionSyntax expression))
                return;

            foreach (Diagnostic diagnostic in context.Diagnostics)
            {
                switch (diagnostic.Id)
                {
                    case CompilerDiagnosticIdentifiers.CannotImplicitlyConvertTypeExplicitConversionExists:
                        {
                            SemanticModel semanticModel = await context.GetSemanticModelAsync().ConfigureAwait(false);

                            TypeInfo typeInfo = semanticModel.GetTypeInfo(expression, context.CancellationToken);

                            ITypeSymbol type = typeInfo.Type;
                            ITypeSymbol convertedType = typeInfo.ConvertedType;

                            if ((type is INamedTypeSymbol namedType)
                                && namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
                            {
                                if (convertedType?.IsBoolean() == true
                                    || AddComparisonWithBooleanLiteralRefactoring.IsCondition(expression))
                                {
                                    if (Settings.IsCodeFixEnabled(CodeFixIdentifiers.AddComparisonWithBooleanLiteral))
                                    {
                                        CodeAction codeAction = CodeAction.Create(
                                            AddComparisonWithBooleanLiteralRefactoring.GetTitle(expression),
                                            cancellationToken => AddComparisonWithBooleanLiteralRefactoring.RefactorAsync(context.Document, expression, cancellationToken),
                                            GetEquivalenceKey(diagnostic, CodeFixIdentifiers.AddComparisonWithBooleanLiteral));

                                        context.RegisterCodeFix(codeAction, diagnostic);
                                    }
                                }
                                else if (namedType.TypeArguments[0].Equals(convertedType))
                                {
                                    if (Settings.IsCodeFixEnabled(CodeFixIdentifiers.UseCoalesceExpression))
                                    {
                                        CodeAction codeAction = CodeAction.Create(
                                            "Use coalesce expression",
                                            cancellationToken =>
                                            {
                                                ExpressionSyntax defaultValue = convertedType.GetDefaultValueSyntax(semanticModel, expression.SpanStart);

                                                ExpressionSyntax newNode = CoalesceExpression(expression.WithoutTrivia(), defaultValue)
                                                    .WithTriviaFrom(expression)
                                                    .Parenthesize()
                                                    .WithFormatterAnnotation();

                                                return context.Document.ReplaceNodeAsync(expression, newNode, cancellationToken);
                                            },
                                            GetEquivalenceKey(diagnostic, CodeFixIdentifiers.UseCoalesceExpression));

                                        context.RegisterCodeFix(codeAction, diagnostic);
                                    }
                                }
                            }

                            if (Settings.IsCodeFixEnabled(CodeFixIdentifiers.ChangeMemberTypeAccordingToReturnExpression)
                                && expression.IsParentKind(SyntaxKind.ReturnStatement, SyntaxKind.YieldReturnStatement))
                            {
                                ChangeMemberTypeRefactoring.ComputeCodeFix(context, diagnostic, expression, semanticModel);
                            }

                            if (Settings.IsCodeFixEnabled(CodeFixIdentifiers.AddCastExpression))
                                CodeFixRegistrator.AddCastExpression(context, diagnostic, expression, convertedType, semanticModel);

                            if (Settings.IsCodeFixEnabled(CodeFixIdentifiers.CreateSingletonArray)
                                && type?.IsErrorType() == false
                                && !type.Equals(convertedType)
                                && (convertedType is IArrayTypeSymbol arrayType)
                                && semanticModel.IsImplicitConversion(expression, arrayType.ElementType))
                            {
                                CodeAction codeAction = CodeAction.Create(
                                    "Create singleton array",
                                    cancellationToken => CreateSingletonArrayRefactoring.RefactorAsync(context.Document, expression, arrayType.ElementType, semanticModel, cancellationToken),
                                    GetEquivalenceKey(diagnostic, CodeFixIdentifiers.CreateSingletonArray));

                                context.RegisterCodeFix(codeAction, diagnostic);
                            }

                            break;
                        }
                    case CompilerDiagnosticIdentifiers.ConstantValueCannotBeConverted:
                        {
                            if (!Settings.IsCodeFixEnabled(CodeFixIdentifiers.UseUncheckedExpression))
                                break;

                            CodeAction codeAction = CodeAction.Create(
                                "Use 'unchecked'",
                                cancellationToken =>
                                {
                                    CheckedExpressionSyntax newNode = CSharpFactory.UncheckedExpression(expression.WithoutTrivia());

                                    newNode = newNode.WithTriviaFrom(expression);

                                    return context.Document.ReplaceNodeAsync(expression, newNode, cancellationToken);
                                },
                                GetEquivalenceKey(diagnostic));

                            context.RegisterCodeFix(codeAction, diagnostic);
                            break;
                        }
                    case CompilerDiagnosticIdentifiers.ExpressionBeingAssignedMustBeConstant:
                        {
                            SyntaxNode parent = expression.Parent;

                            if (parent?.IsKind(SyntaxKind.EqualsValueClause) != true)
                                break;

                            parent = parent.Parent;

                            if (parent?.IsKind(SyntaxKind.VariableDeclarator) != true)
                                break;

                            parent = parent.Parent;

                            if (!(parent is VariableDeclarationSyntax variableDeclaration))
                                break;

                            if (Settings.IsCodeFixEnabled(CodeFixIdentifiers.RemoveConstModifier)
                                && variableDeclaration.Parent is LocalDeclarationStatementSyntax localDeclarationStatement)
                            {
                                SyntaxTokenList modifiers = localDeclarationStatement.Modifiers;

                                if (!modifiers.Contains(SyntaxKind.ConstKeyword))
                                    break;

                                ModifiersCodeFixRegistrator.RemoveModifier(context, diagnostic, localDeclarationStatement, SyntaxKind.ConstKeyword);
                            }
                            else if (Settings.IsCodeFixEnabled(CodeFixIdentifiers.ReplaceConstantWithField)
                                && variableDeclaration.Variables.Count == 1
                                && (variableDeclaration.Parent is FieldDeclarationSyntax fieldDeclaration)
                                && fieldDeclaration.Modifiers.Contains(SyntaxKind.ConstKeyword))
                            {
                                CodeAction codeAction = CodeAction.Create(
                                    ReplaceConstantWithFieldRefactoring.Title,
                                    cancellationToken => ReplaceConstantWithFieldRefactoring.RefactorAsync(context.Document, fieldDeclaration, cancellationToken),
                                    GetEquivalenceKey(diagnostic));

                                context.RegisterCodeFix(codeAction, diagnostic);
                                break;
                            }

                            break;
                        }
                    case CompilerDiagnosticIdentifiers.CannotConvertNullToTypeBecauseItIsNonNullableValueType:
                    case CompilerDiagnosticIdentifiers.CannotConvertNullToTypeParameterBecauseItCouldBeNonNullableValueType:
                        {
                            if (!Settings.IsCodeFixEnabled(CodeFixIdentifiers.ReplaceNullLiteralExpressionWithDefaultValue))
                                break;

                            SemanticModel semanticModel = await context.GetSemanticModelAsync().ConfigureAwait(false);

                            CodeFixRegistrator.ReplaceNullWithDefaultValue(context, diagnostic, expression, semanticModel);
                            break;
                        }
                    case CompilerDiagnosticIdentifiers.ResultOfExpressionIsAlwaysConstantSinceValueIsNeverEqualToNull:
                        {
                            if (!Settings.IsCodeFixEnabled(CodeFixIdentifiers.RemoveConditionThatIsAlwaysEqualToTrueOrFalse))
                                break;

                            NullCheckExpressionInfo nullCheck = SyntaxInfo.NullCheckExpressionInfo(expression, allowedKinds: NullCheckKind.ComparisonToNull);

                            if (!nullCheck.Success)
                                break;

                            CodeAction codeAction = CodeAction.Create(
                                "Remove condition",
                                cancellationToken =>
                                {
                                    cancellationToken.ThrowIfCancellationRequested();

                                    SyntaxNode newRoot = RemoveCondition(root, expression, nullCheck.Kind == NullCheckKind.NotEqualsToNull);

                                    cancellationToken.ThrowIfCancellationRequested();

                                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                                },
                                GetEquivalenceKey(diagnostic));

                            context.RegisterCodeFix(codeAction, diagnostic);

                            break;
                        }
                    case CompilerDiagnosticIdentifiers.OnlyAssignmentCallIncrementDecrementAndNewObjectExpressionsCanBeUsedAsStatement:
                        {
                            SemanticModel semanticModel = await context.GetSemanticModelAsync().ConfigureAwait(false);

                            if (expression.Parent is ArrowExpressionClauseSyntax arrowExpresssionClause)
                            {
                                if (!Settings.IsCodeFixEnabled(CodeFixIdentifiers.ChangeMemberTypeAccordingToReturnExpression))
                                    break;

                                ChangeMemberTypeRefactoring.ComputeCodeFix(context, diagnostic, expression, semanticModel);
                            }
                            else if (expression.Parent is ExpressionStatementSyntax expressionStatement)
                            {
                                if (Settings.IsCodeFixEnabled(CodeFixIdentifiers.AddArgumentList)
                                    && expression.IsKind(
                                        SyntaxKind.IdentifierName,
                                        SyntaxKind.SimpleMemberAccessExpression))
                                {
                                    SyntaxNode invocationExpression = InvocationExpression(expression);

                                    if (semanticModel.GetSpeculativeMethodSymbol(expression.SpanStart, invocationExpression) != null)
                                    {
                                        CodeAction codeAction = CodeAction.Create(
                                            "Add argument list",
                                            cancellationToken => context.Document.ReplaceNodeAsync(expression, invocationExpression, cancellationToken),
                                            GetEquivalenceKey(diagnostic, CodeFixIdentifiers.AddArgumentList));

                                        context.RegisterCodeFix(codeAction, diagnostic);
                                    }
                                }

                                if (Settings.IsCodeFixEnabled(CodeFixIdentifiers.ReplaceComparisonWithAssignment)
                                    && expression.IsKind(SyntaxKind.EqualsExpression))
                                {
                                    BinaryExpressionInfo info = SyntaxInfo.BinaryExpressionInfo(expression);

                                    if (!info.Success)
                                        break;

                                    ITypeSymbol typeSymbol = semanticModel.GetTypeSymbol(info.Left, context.CancellationToken);

                                    if (typeSymbol?.IsErrorType() != false)
                                        break;

                                    if (!semanticModel.IsImplicitConversion(info.Right, typeSymbol))
                                        break;

                                    CodeAction codeAction = CodeAction.Create(
                                        "Replace comparison with assignment",
                                        cancellationToken =>
                                        {
                                            AssignmentExpressionSyntax simpleAssignment = SimpleAssignmentExpression(info.Left, info.Right).WithTriviaFrom(expression);
                                            return context.Document.ReplaceNodeAsync(expression, simpleAssignment, cancellationToken);
                                        },
                                        GetEquivalenceKey(diagnostic, CodeFixIdentifiers.ReplaceComparisonWithAssignment));

                                    context.RegisterCodeFix(codeAction, diagnostic);
                                }

                                if (Settings.IsCodeFixEnabled(CodeFixIdentifiers.ReplaceConditionalExpressionWithIfElse)
                                    && (expression is ConditionalExpressionSyntax conditionalExpression)
                                    && conditionalExpression.Condition != null)
                                {
                                    ExpressionSyntax whenTrue = conditionalExpression.WhenTrue;
                                    ExpressionSyntax whenFalse = conditionalExpression.WhenFalse;

                                    if (whenTrue != null
                                        && whenFalse != null
                                        && semanticModel.GetTypeSymbol(whenTrue, context.CancellationToken)?.IsVoid() == true
                                        && semanticModel.GetTypeSymbol(whenFalse, context.CancellationToken)?.IsVoid() == true)
                                    {
                                        CodeAction codeAction = CodeAction.Create(
                                            "Replace ?: with if-else",
                                            cancellationToken =>
                                            {
                                                IfStatementSyntax newNode = IfStatement(
                                                    conditionalExpression.Condition.WalkDownParentheses(),
                                                    Block(ExpressionStatement(whenTrue)),
                                                    ElseClause(Block(ExpressionStatement(whenFalse))));

                                                newNode = newNode
                                                    .WithTriviaFrom(expressionStatement)
                                                    .WithFormatterAnnotation();

                                                return context.Document.ReplaceNodeAsync(expressionStatement, newNode, cancellationToken);
                                            },
                                            GetEquivalenceKey(diagnostic, CodeFixIdentifiers.ReplaceConditionalExpressionWithIfElse));

                                        context.RegisterCodeFix(codeAction, diagnostic);
                                    }
                                }

                                if (Settings.IsAnyCodeFixEnabled(
                                    CodeFixIdentifiers.IntroduceLocalVariable,
                                    CodeFixIdentifiers.IntroduceField))
                                {
                                    if (semanticModel.GetSymbol(expression, context.CancellationToken)?.IsErrorType() != false)
                                        break;

                                    ITypeSymbol typeSymbol = semanticModel.GetTypeSymbol(expression, context.CancellationToken);

                                    if (typeSymbol?.IsErrorType() != false)
                                        break;

                                    if (Settings.IsCodeFixEnabled(CodeFixIdentifiers.IntroduceLocalVariable)
                                        && !expressionStatement.IsEmbedded())
                                    {
                                        bool addAwait = typeSymbol.IsConstructedFromTaskOfT(semanticModel)
                                            && semanticModel.GetEnclosingSymbol(expressionStatement.SpanStart, context.CancellationToken).IsAsyncMethod();

                                        CodeAction codeAction = CodeAction.Create(
                                            IntroduceLocalVariableRefactoring.GetTitle(expression),
                                            cancellationToken => IntroduceLocalVariableRefactoring.RefactorAsync(context.Document, expressionStatement, typeSymbol, addAwait, semanticModel, cancellationToken),
                                            GetEquivalenceKey(diagnostic, CodeFixIdentifiers.IntroduceLocalVariable));

                                        context.RegisterCodeFix(codeAction, diagnostic);
                                    }

                                    if (Settings.IsCodeFixEnabled(CodeFixIdentifiers.IntroduceField))
                                    {
                                        CodeAction codeAction = CodeAction.Create(
                                            $"Introduce field for '{expression}'",
                                            cancellationToken => IntroduceFieldRefactoring.RefactorAsync(context.Document, expressionStatement, typeSymbol, semanticModel, cancellationToken),
                                            GetEquivalenceKey(diagnostic, CodeFixIdentifiers.IntroduceField));

                                        context.RegisterCodeFix(codeAction, diagnostic);
                                    }
                                }
                            }

                            break;
                        }
                    case CompilerDiagnosticIdentifiers.CannotImplicitlyConvertType:
                        {
                            if (Settings.IsCodeFixEnabled(CodeFixIdentifiers.ReplaceYieldReturnWithForEach)
                                && expression.IsParentKind(SyntaxKind.YieldReturnStatement))
                            {
                                SemanticModel semanticModel = await context.GetSemanticModelAsync().ConfigureAwait(false);
                                ReplaceYieldReturnWithForEachRefactoring.ComputeCodeFix(context, diagnostic, expression, semanticModel);
                                break;
                            }

                            if (Settings.IsCodeFixEnabled(CodeFixIdentifiers.ChangeMemberTypeAccordingToReturnExpression)
                                && expression.IsParentKind(SyntaxKind.ReturnStatement, SyntaxKind.YieldReturnStatement))
                            {
                                SemanticModel semanticModel = await context.GetSemanticModelAsync().ConfigureAwait(false);

                                ChangeMemberTypeRefactoring.ComputeCodeFix(context, diagnostic, expression, semanticModel);
                                break;
                            }

                            if (Settings.IsCodeFixEnabled(CodeFixIdentifiers.ChangeTypeAccordingToInitializer))
                            {
                                SemanticModel semanticModel = await context.GetSemanticModelAsync().ConfigureAwait(false);

                                ChangeTypeAccordingToInitializerRefactoring.ComputeCodeFix(context, diagnostic, expression, semanticModel);
                                break;
                            }

                            if (Settings.IsCodeFixEnabled(CodeFixIdentifiers.ReplaceStringLiteralWithCharacterLiteral)
                                && expression?.Kind() == SyntaxKind.StringLiteralExpression)
                            {
                                var literalExpression = (LiteralExpressionSyntax)expression;

                                if (literalExpression.Token.ValueText.Length == 1)
                                {
                                    SemanticModel semanticModel = await context.GetSemanticModelAsync().ConfigureAwait(false);

                                    if (semanticModel.GetTypeInfo(expression, context.CancellationToken).ConvertedType?.IsChar() == true)
                                    {
                                        CodeAction codeAction = CodeAction.Create(
                                            "Replace string literal with character literal",
                                            cancellationToken => ReplaceStringLiteralWithCharacterLiteralRefactoring.RefactorAsync(context.Document, literalExpression, cancellationToken),
                                            GetEquivalenceKey(diagnostic, CodeFixIdentifiers.ReplaceStringLiteralWithCharacterLiteral));

                                        context.RegisterCodeFix(codeAction, diagnostic);
                                    }
                                }
                            }

                            if (Settings.IsCodeFixEnabled(CodeFixIdentifiers.UseYieldReturnInsteadOfReturn)
                                && expression.IsParentKind(SyntaxKind.ReturnStatement))
                            {
                                var returnStatement = (ReturnStatementSyntax)expression.Parent;

                                SemanticModel semanticModel = await context.GetSemanticModelAsync().ConfigureAwait(false);

                                ISymbol containingSymbol = semanticModel.GetEnclosingSymbol(returnStatement.SpanStart, context.CancellationToken);

                                if (containingSymbol?.IsKind(SymbolKind.Method) == true
                                    && ((IMethodSymbol)containingSymbol).ReturnType?.IsIEnumerableOrConstructedFromIEnumerableOfT() == true)
                                {
                                    CodeAction codeAction = CodeAction.Create(
                                        "Use yield return instead of return",
                                        cancellationToken => UseYieldReturnInsteadOfReturnRefactoring.RefactorAsync(context.Document, returnStatement, SyntaxKind.YieldReturnStatement, semanticModel, cancellationToken),
                                        GetEquivalenceKey(diagnostic, CodeFixIdentifiers.UseYieldReturnInsteadOfReturn));

                                    context.RegisterCodeFix(codeAction, diagnostic);
                                }
                            }

                            break;
                        }
                }
            }
        }

        private static TNode RemoveCondition<TNode>(
            TNode node,
            ExpressionSyntax expression,
            bool isTrue) where TNode : SyntaxNode
        {
            expression = expression.WalkUpParentheses();

            SyntaxNode parent = expression.Parent;

            switch (parent?.Kind())
            {
                case SyntaxKind.IfStatement:
                    {
                        var ifStatement = (IfStatementSyntax)parent;

                        if (ifStatement.Condition == expression)
                        {
                            if (isTrue)
                            {
                                return RemoveOrReplaceNode(node, ifStatement, ifStatement.Statement);
                            }
                            else
                            {
                                return RemoveOrReplaceNode(node, ifStatement, ifStatement.Else?.Statement);
                            }
                        }

                        break;
                    }
                case SyntaxKind.DoStatement:
                    {
                        var doStatement = (DoStatementSyntax)parent;

                        if (doStatement.Condition == expression
                            && !isTrue)
                        {
                            return RemoveOrReplaceNode(node, doStatement, doStatement.Statement);
                        }

                        break;
                    }
                case SyntaxKind.WhileStatement:
                    {
                        var whileStatement = (WhileStatementSyntax)parent;

                        if (whileStatement.Condition == expression
                            && !isTrue)
                        {
                            return node.RemoveStatement(whileStatement);
                        }

                        break;
                    }
                case SyntaxKind.ForStatement:
                    {
                        var forStatement = (ForStatementSyntax)parent;

                        if (forStatement.Condition == expression)
                        {
                            if (isTrue)
                            {
                                return node.RemoveNode(expression, SyntaxRemover.GetOptions(expression));
                            }
                            else
                            {
                                return node.RemoveStatement(forStatement);
                            }
                        }

                        break;
                    }
                case SyntaxKind.ConditionalExpression:
                    {
                        var conditionalExpression = (ConditionalExpressionSyntax)parent;

                        if (conditionalExpression.Condition == expression)
                        {
                            if (isTrue)
                            {
                                return RemoveOrReplaceNode(node, conditionalExpression, conditionalExpression.WhenTrue);
                            }
                            else
                            {
                                return RemoveOrReplaceNode(node, conditionalExpression, conditionalExpression.WhenFalse);
                            }
                        }

                        break;
                    }
                case SyntaxKind.LogicalAndExpression:
                    {
                        var logicalAnd = (BinaryExpressionSyntax)parent;

                        ExpressionSyntax left = logicalAnd.Left;
                        ExpressionSyntax right = logicalAnd.Right;

                        ExpressionSyntax other = (left == expression) ? right : left;

                        if (isTrue)
                        {
                            return RemoveOrReplaceNode(node, logicalAnd, other);
                        }
                        else
                        {
                            return RemoveCondition(node, logicalAnd, isTrue);
                        }
                    }
                case SyntaxKind.LogicalOrExpression:
                    {
                        var logicalOr = (BinaryExpressionSyntax)parent;

                        ExpressionSyntax left = logicalOr.Left;
                        ExpressionSyntax right = logicalOr.Right;

                        ExpressionSyntax other = (left == expression) ? right : left;

                        if (isTrue)
                        {
                            return RemoveCondition(node, logicalOr, isTrue);
                        }
                        else
                        {
                            return RemoveOrReplaceNode(node, logicalOr, other);
                        }
                    }
            }

            return node.ReplaceNode(expression, BooleanLiteralExpression(isTrue));

            TRoot RemoveOrReplaceNode<TRoot>(
                TRoot root,
                SyntaxNode nodeToRemove,
                SyntaxNode newNode) where TRoot : SyntaxNode
            {
                if (newNode == null)
                {
                    if (nodeToRemove == null)
                        return root;

                    if (nodeToRemove is StatementSyntax statement)
                        root.RemoveStatement(statement);

                    return root.RemoveNode(nodeToRemove, SyntaxRemover.GetOptions(nodeToRemove));
                }

                if (newNode is BlockSyntax block)
                    return root.ReplaceNode(nodeToRemove, block.Statements);

                return root.ReplaceNode(nodeToRemove, newNode);
            }
        }
    }
}
