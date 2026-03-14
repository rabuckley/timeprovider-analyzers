using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TimeProviderAnalyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PassTimeProviderCodeFixProvider)), Shared]
public class PassTimeProviderCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create(Rules.PassTimeProviderDescriptor.Id);

    public sealed override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
            return;

        var diagnostic = context.Diagnostics[0];
        var diagnosticSpan = diagnostic.Location.SourceSpan;
        var node = root.FindNode(diagnosticSpan);

        var timeProviderName = diagnostic.Properties.GetValueOrDefault("TimeProviderName");
        var timeProviderParameterName = diagnostic.Properties.GetValueOrDefault("TimeProviderParameterName");

        if (timeProviderName is null || timeProviderParameterName is null)
            return;

        if (!int.TryParse(diagnostic.Properties.GetValueOrDefault("TimeProviderParameterIndex"), out int parameterIndex))
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.PassTimeProviderCodeFixTitle,
                createChangedDocument: ct => AddTimeProviderArgumentAsync(
                    context.Document, node, timeProviderName, parameterIndex, timeProviderParameterName, ct),
                equivalenceKey: nameof(CodeFixResources.PassTimeProviderCodeFixTitle)),
            diagnostic);
    }

    private static async Task<Document> AddTimeProviderArgumentAsync(
        Document document,
        SyntaxNode node,
        string timeProviderName,
        int parameterIndex,
        string timeProviderParameterName,
        CancellationToken cancellationToken)
    {
        var timeProviderExpression = SyntaxFactory.IdentifierName(timeProviderName);

        SyntaxNode newNode = node switch
        {
            InvocationExpressionSyntax invocation => invocation.WithArgumentList(
                InsertArgument(invocation.ArgumentList, timeProviderExpression, parameterIndex, timeProviderParameterName)),

            ObjectCreationExpressionSyntax creation => creation.WithArgumentList(
                InsertArgument(
                    creation.ArgumentList ?? SyntaxFactory.ArgumentList(),
                    timeProviderExpression, parameterIndex, timeProviderParameterName)),

            _ => node
        };

        if (newNode == node)
            return document;

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var newRoot = root!.ReplaceNode(node, newNode);

        return document.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    /// Inserts a TimeProvider argument at the correct position in the argument list.
    /// If any existing argument uses named syntax, the inserted argument is also named.
    /// </summary>
    private static ArgumentListSyntax InsertArgument(
        ArgumentListSyntax argumentList,
        ExpressionSyntax timeProviderExpression,
        int parameterIndex,
        string timeProviderParameterName)
    {
        var hasNamedArguments = argumentList.Arguments.Any(a => a.NameColon is not null);

        var argument = hasNamedArguments
            ? SyntaxFactory.Argument(
                SyntaxFactory.NameColon(timeProviderParameterName),
                default,
                timeProviderExpression)
            : SyntaxFactory.Argument(timeProviderExpression);

        var arguments = argumentList.Arguments;

        // Insert at the correct position. If the index is beyond the current argument count,
        // append at the end (the common case for trailing TimeProvider parameters).
        var insertIndex = parameterIndex <= arguments.Count ? parameterIndex : arguments.Count;

        return argumentList.WithArguments(arguments.Insert(insertIndex, argument));
    }
}
