using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace TimeProviderAnalyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TimeProviderAnalyzersAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        Rules.UseOfStaticTimeDescriptor,
        Rules.UseOfStaticTimeWithTimeProviderInScopeDescriptor,
        Rules.PassTimeProviderDescriptor
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationStartAnalysisContext compilationContext)
    {
        if (!compilationContext.Compilation.TryGetTypeByMetadataName(
                WellKnownTypeNames.TimeProvider, out var timeProviderSymbol))
        {
            return;
        }

        if (!compilationContext.Compilation.TryGetTypeByMetadataName(
                WellKnownTypeNames.DateTime, out var dateTimeSymbol))
        {
            return;
        }

        if (!compilationContext.Compilation.TryGetTypeByMetadataName(
                WellKnownTypeNames.DateTimeOffset, out var dateTimeOffsetSymbol))
        {
            return;
        }

        var symbols = new TargetSymbols
        {
            TimeProviderSymbol = timeProviderSymbol!,
            DateTimeOffsetSymbol = dateTimeOffsetSymbol!,
            DateTimeSymbol = dateTimeSymbol!,
        };

        compilationContext.RegisterSyntaxNodeAction(
            context => AnalyzeMemberAccess(context, symbols),
            SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeMemberAccess(
        SyntaxNodeAnalysisContext context,
        TargetSymbols symbols)
    {
        var memberAccessExpression = (MemberAccessExpressionSyntax)context.Node;
        var semanticModel = context.SemanticModel;
        var symbolInfo = semanticModel.GetSymbolInfo(memberAccessExpression);

        if (symbolInfo.Symbol is not IPropertySymbol propertySymbol
            || !SymbolIsStaticAccessTarget(symbols, propertySymbol))
        {
            return;
        }

        if (TryGetAccessibleTimeProvider(semanticModel, symbols.TimeProviderSymbol, memberAccessExpression,
                out string? timeProviderName))
        {
            ReportDiagnostic(context, new UseOfStaticTimeWithTimeProviderInScopeAnalyzerContext
            {
                TimeProviderName = timeProviderName!,
                PropertyName = propertySymbol.GetContainingTypeQualifiedPropertyName(),
                Location = memberAccessExpression.GetLocation()
            });

            return;
        }

        ReportDiagnostic(context, new UseOfStaticTimeAnalyzerContext
        {
            PropertyName = propertySymbol.GetContainingTypeQualifiedPropertyName(), 
            Location = memberAccessExpression.GetLocation()
        });
    }

    private static bool SymbolIsStaticAccessTarget(TargetSymbols symbols, IPropertySymbol propertySymbol)
    {
        if (SymbolEqualityComparer.Default.Equals(propertySymbol.ContainingType, symbols.DateTimeSymbol)
            && TargetPropertyNames.DateTimePropertyNames.Contains(propertySymbol.Name))
        {
            return true;
        }

        if (SymbolEqualityComparer.Default.Equals(propertySymbol.ContainingType, symbols.DateTimeOffsetSymbol)
            || TargetPropertyNames.DateTimeOffsetPropertyNames.Contains(propertySymbol.Name))
        {
            return true;
        }

        return false;
    }

    private static void ReportDiagnostic(SyntaxNodeAnalysisContext context, AnalyzerContext analyzerContext)
    {
        var diagnostic = analyzerContext switch
        {
            UseOfStaticTimeWithTimeProviderInScopeAnalyzerContext c => Diagnostic.Create(
                Rules.UseOfStaticTimeWithTimeProviderInScopeDescriptor,
                c.Location,
                c.TimeProviderName,
                c.PropertyName),
            UseOfStaticTimeAnalyzerContext c => Diagnostic.Create(
                Rules.UseOfStaticTimeDescriptor,
                c.Location,
                c.PropertyName),
            _ => null
        };

        if (diagnostic is null)
        {
            return;
        }

        context.ReportDiagnostic(diagnostic);
    }

    private static bool TryGetAccessibleTimeProvider(
        SemanticModel semanticModel,
        INamedTypeSymbol timeProviderSymbol,
        ExpressionSyntax expression,
        out string? timeProviderName)
    {
        // Check if TimeProvider is in scope by looking for local variables, parameters, or fields
        if (expression.FirstAncestorOrSelf<MethodDeclarationSyntax>() is null)
        {
            timeProviderName = null;
            return false;
        }

        if (expression.Ancestors().FirstOrDefault(a => a is BaseMethodDeclarationSyntax) is not
            BaseMethodDeclarationSyntax methodDeclarationSyntax)
        {
            timeProviderName = null;
            return false;
        }

        return TryGetTimeProviderParameter(semanticModel, methodDeclarationSyntax, timeProviderSymbol, out timeProviderName)
            || TryGetLocallyDeclaredTimeProvider(semanticModel, expression, methodDeclarationSyntax, timeProviderSymbol, out timeProviderName)
            || TryGetTimeProviderFieldOrProperty(semanticModel, methodDeclarationSyntax, timeProviderSymbol, out timeProviderName);
    }

    private static bool TryGetTimeProviderFieldOrProperty(
        SemanticModel semanticModel,
        BaseMethodDeclarationSyntax methodDeclarationSyntax,
        INamedTypeSymbol timeProviderSymbol,
        out string? timeProviderName)
    {
        var containingType = methodDeclarationSyntax.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();

        if (containingType is null)
        {
            timeProviderName = null;
            return false;
        }

        var fieldDeclarations = containingType.DescendantNodes().OfType<FieldDeclarationSyntax>();

        foreach (var fieldDeclaration in fieldDeclarations)
        {
            foreach (var variable in fieldDeclaration.Declaration.Variables)
            {
                var fieldSymbol = semanticModel.GetDeclaredSymbol(variable);

                if (fieldSymbol is IFieldSymbol field && IsTimeProviderType(field.Type, timeProviderSymbol))
                {
                    timeProviderName = variable.Identifier.Text;
                    return true;
                }
            }
        }

        var propertyDeclarations = containingType.DescendantNodes().OfType<PropertyDeclarationSyntax>();

        foreach (var propertyDeclaration in propertyDeclarations)
        {
            var propertySymbol = semanticModel.GetDeclaredSymbol(propertyDeclaration);

            if (propertySymbol is not null && IsTimeProviderType(propertySymbol.Type, timeProviderSymbol))
            {
                timeProviderName = propertyDeclaration.Identifier.Text;
                return true;
            }
        }

        timeProviderName = null;
        return false;
    }

    private static bool TryGetLocallyDeclaredTimeProvider(
        SemanticModel semanticModel,
        ExpressionSyntax expression,
        BaseMethodDeclarationSyntax methodDeclarationSyntax,
        INamedTypeSymbol timeProviderSymbol,
        out string? timeProviderName)
    {
        var localDeclarations = methodDeclarationSyntax
            .DescendantNodes()
            .TakeWhile(n => n.SpanStart < expression.SpanStart)
            .OfType<VariableDeclarationSyntax>();

        foreach (var declaration in localDeclarations)
        {
            foreach (var variable in declaration.Variables)
            {
                var variableSymbol = semanticModel.GetDeclaredSymbol(variable);

                if (variableSymbol is ILocalSymbol localSymbol
                    && IsTimeProviderType(localSymbol.Type, timeProviderSymbol))
                {
                    timeProviderName = variable.Identifier.Text;
                    return true;
                }
            }
        }

        timeProviderName = null;
        return false;
    }

    private static bool TryGetTimeProviderParameter(
        SemanticModel semanticModel,
        BaseMethodDeclarationSyntax methodDeclarationSyntax,
        INamedTypeSymbol timeProviderSymbol,
        out string? timeProviderName)
    {
        foreach (var parameter in methodDeclarationSyntax.ParameterList.Parameters)
        {
            var parameterSymbol = semanticModel.GetDeclaredSymbol(parameter);

            if (parameterSymbol is not null && IsTimeProviderType(parameterSymbol.Type, timeProviderSymbol))
            {
                timeProviderName = parameter.Identifier.Text;
                return true;
            }
        }

        timeProviderName = null;
        return false;
    }


    private static bool IsTimeProviderType(ITypeSymbol typeSymbol, INamedTypeSymbol timeProviderSymbol)
    {
        return SymbolEqualityComparer.Default.Equals(typeSymbol, timeProviderSymbol)
            || (typeSymbol is INamedTypeSymbol namedType
            && namedType.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, timeProviderSymbol)));
    }
}
