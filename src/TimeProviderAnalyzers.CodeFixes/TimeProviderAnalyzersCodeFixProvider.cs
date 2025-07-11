﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TimeProviderAnalyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TimeProviderAnalyzersCodeFixProvider)), Shared]
public class TimeProviderAnalyzersCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create(Rules.UseOfStaticTimeWithTimeProviderInScopeDescriptor.Id);

    public sealed override FixAllProvider GetFixAllProvider()
    {
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        return WellKnownFixAllProviders.BatchFixer;
    }

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
            return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Find the type declaration identified by the diagnostic.
        var declaration = root.FindToken(diagnosticSpan.Start)
            .Parent?.AncestorsAndSelf()
            .OfType<TypeDeclarationSyntax>()
            .First();

        // No `Parent`
        if (declaration is null)
            return;

        // Register a code action that will invoke the fix.
        context.RegisterCodeFix(
            CodeAction.Create(
                title: CodeFixResources.CodeFixTitle,
                createChangedSolution: c => MakeUppercaseAsync(context.Document, declaration, c),
                equivalenceKey: nameof(CodeFixResources.CodeFixTitle)),
            diagnostic);
    }

    private async Task<Solution> MakeUppercaseAsync(
        Document document,
        TypeDeclarationSyntax typeDecl,
        CancellationToken cancellationToken)
    {
        // Compute new uppercase name.
        var identifierToken = typeDecl.Identifier;
        var newName = identifierToken.Text.ToUpperInvariant();

        // Get the symbol representing the type to be renamed.
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken)!;

        var renameOptions = new SymbolRenameOptions();

        var sln = await Renamer
            .RenameSymbolAsync(document.Project.Solution, typeSymbol, renameOptions, newName, cancellationToken)
            .ConfigureAwait(false);

        // Return the new solution with the now-uppercase type name.
        return sln;
    }
}
