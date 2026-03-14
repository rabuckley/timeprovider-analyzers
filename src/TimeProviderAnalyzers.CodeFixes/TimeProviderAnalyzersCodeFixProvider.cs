using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace TimeProviderAnalyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TimeProviderAnalyzersCodeFixProvider)), Shared]
public class TimeProviderAnalyzersCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create(Rules.UseOfStaticTimeWithTimeProviderInScopeDescriptor.Id);

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

        if (node is not MemberAccessExpressionSyntax memberAccess)
            return;

        var timeProviderName = diagnostic.Properties.GetValueOrDefault("TimeProviderName");
        var propertyName = diagnostic.Properties.GetValueOrDefault("PropertyName");

        if (timeProviderName is null || propertyName is null)
            return;

        if (!TimeProviderReplacements.Expressions.ContainsKey(propertyName))
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.CodeFixTitle,
                createChangedDocument: ct => ReplaceWithTimeProviderCallAsync(
                    context.Document, memberAccess, timeProviderName, propertyName, ct),
                equivalenceKey: nameof(CodeFixResources.CodeFixTitle)),
            diagnostic);
    }

    private static async Task<Document> ReplaceWithTimeProviderCallAsync(
        Document document,
        MemberAccessExpressionSyntax memberAccess,
        string timeProviderName,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var replacementTemplate = TimeProviderReplacements.Expressions[propertyName];
        var replacementText = replacementTemplate.Replace("{0}", timeProviderName);

        var replacementExpression = SyntaxFactory.ParseExpression(replacementText)
            .WithTriviaFrom(memberAccess);

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var newRoot = root!.ReplaceNode(memberAccess, replacementExpression);

        return document.WithSyntaxRoot(newRoot);
    }
}
