using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TimeProviderAnalyzers;

/// <summary>
/// Code fix for TPA0001: adds a <c>TimeProvider timeProvider</c> parameter to the enclosing method
/// and replaces the static DateTime/DateTimeOffset property access with the TimeProvider equivalent.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddTimeProviderParameterCodeFixProvider)), Shared]
public class AddTimeProviderParameterCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create(Rules.UseOfStaticTimeDescriptor.Id);

    public sealed override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
            return;

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not MemberAccessExpressionSyntax memberAccess)
            return;

        var propertyName = diagnostic.Properties.GetValueOrDefault("PropertyName");

        if (propertyName is null || !TimeProviderReplacements.Expressions.ContainsKey(propertyName))
            return;

        // Only offer the fix when there is an enclosing method or constructor to add the parameter to.
        if (memberAccess.Ancestors().FirstOrDefault(a => a is BaseMethodDeclarationSyntax) is not
            BaseMethodDeclarationSyntax)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.AddTimeProviderParameterCodeFixTitle,
                createChangedDocument: ct => AddParameterAndReplaceAsync(
                    context.Document, memberAccess, propertyName, ct),
                equivalenceKey: nameof(CodeFixResources.AddTimeProviderParameterCodeFixTitle)),
            diagnostic);
    }

    private static async Task<Document> AddParameterAndReplaceAsync(
        Document document,
        MemberAccessExpressionSyntax memberAccess,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        var enclosingMethod = memberAccess.Ancestors()
            .OfType<BaseMethodDeclarationSyntax>()
            .First();

        // Add TimeProvider timeProvider as the last parameter.
        const string parameterName = "timeProvider";
        var timeProviderParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName))
            .WithType(SyntaxFactory.ParseTypeName("TimeProvider").WithTrailingTrivia(SyntaxFactory.Space));

        var newParameterList = enclosingMethod.ParameterList.AddParameters(timeProviderParameter);
        editor.ReplaceNode(enclosingMethod.ParameterList, newParameterList);

        // Replace the static property access with the TimeProvider call.
        var replacementTemplate = TimeProviderReplacements.Expressions[propertyName];
        var replacementText = replacementTemplate.Replace("{0}", parameterName);
        var replacementExpression = SyntaxFactory.ParseExpression(replacementText)
            .WithTriviaFrom(memberAccess);

        editor.ReplaceNode(memberAccess, replacementExpression);

        return editor.GetChangedDocument();
    }
}
