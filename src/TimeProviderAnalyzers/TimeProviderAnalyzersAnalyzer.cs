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

        compilationContext.RegisterSyntaxNodeAction(
            context => AnalyzeInvocation(context, symbols),
            SyntaxKind.InvocationExpression,
            SyntaxKind.ObjectCreationExpression);
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

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        TargetSymbols symbols)
    {
        var semanticModel = context.SemanticModel;
        var symbolInfo = semanticModel.GetSymbolInfo(context.Node);

        if (symbolInfo.Symbol is not IMethodSymbol calledMethod)
        {
            return;
        }

        // Skip if the method already has a TimeProvider parameter — the caller is already using the right overload.
        if (calledMethod.Parameters.Any(p => IsTimeProviderType(p.Type, symbols.TimeProviderSymbol)))
        {
            return;
        }

        if (!HasOverloadWithTimeProvider(calledMethod, symbols.TimeProviderSymbol,
                out int timeProviderParameterIndex, out string timeProviderParameterName))
        {
            return;
        }

        var expression = (ExpressionSyntax)context.Node;

        if (!TryGetAccessibleTimeProvider(semanticModel, symbols.TimeProviderSymbol, expression,
                out string? timeProviderName))
        {
            return;
        }

        // Format the method name as "Type.Method" for regular methods or "Type" for constructors.
        var containingTypeName = calledMethod.ContainingType.Name;
        var methodDisplayName = calledMethod.MethodKind is MethodKind.Constructor
            ? $"{containingTypeName}"
            : $"{containingTypeName}.{calledMethod.Name}";

        ReportDiagnostic(context, new PassTimeProviderAnalyzerContext
        {
            TimeProviderName = timeProviderName!,
            MethodName = methodDisplayName,
            TimeProviderParameterIndex = timeProviderParameterIndex,
            TimeProviderParameterName = timeProviderParameterName,
            Location = context.Node.GetLocation()
        });
    }

    /// <summary>
    /// Checks whether the called method's containing type has an overload with the same parameters
    /// plus one additional <see cref="TimeProvider"/> parameter.
    /// </summary>
    private static bool HasOverloadWithTimeProvider(
        IMethodSymbol calledMethod,
        INamedTypeSymbol timeProviderSymbol,
        out int timeProviderParameterIndex,
        out string timeProviderParameterName)
    {
        var containingType = calledMethod.ContainingType;
        var calledParams = calledMethod.Parameters;

        var candidateMethods = calledMethod.MethodKind is MethodKind.Constructor
            ? containingType.InstanceConstructors.Cast<IMethodSymbol>()
            : containingType.GetMembers(calledMethod.Name).OfType<IMethodSymbol>();

        foreach (var candidate in candidateMethods)
        {
            if (SymbolEqualityComparer.Default.Equals(candidate, calledMethod))
            {
                continue;
            }

            // Look for an overload that has exactly one more parameter than the called method,
            // where all existing parameters match in order and the extra one is a TimeProvider.
            if (candidate.Parameters.Length != calledParams.Length + 1)
            {
                continue;
            }

            if (IsOverloadWithExtraTimeProvider(calledParams, candidate.Parameters, timeProviderSymbol,
                    out int index))
            {
                timeProviderParameterIndex = index;
                timeProviderParameterName = candidate.Parameters[index].Name;
                return true;
            }
        }

        timeProviderParameterIndex = -1;
        timeProviderParameterName = "";
        return false;
    }

    /// <summary>
    /// Returns true when <paramref name="candidateParams"/> contains all of <paramref name="originalParams"/>
    /// in order, plus exactly one additional parameter whose type is assignable to <see cref="TimeProvider"/>.
    /// The extra parameter can appear at any position.
    /// </summary>
    /// <summary>
    /// Returns true when <paramref name="candidateParams"/> contains all of <paramref name="originalParams"/>
    /// in order, plus exactly one additional parameter whose type is assignable to <see cref="TimeProvider"/>.
    /// The extra parameter can appear at any position. Its index is returned via <paramref name="timeProviderIndex"/>.
    /// </summary>
    private static bool IsOverloadWithExtraTimeProvider(
        ImmutableArray<IParameterSymbol> originalParams,
        ImmutableArray<IParameterSymbol> candidateParams,
        INamedTypeSymbol timeProviderSymbol,
        out int timeProviderIndex)
    {
        int originalIndex = 0;
        timeProviderIndex = -1;

        for (int i = 0; i < candidateParams.Length; i++)
        {
            if (timeProviderIndex < 0 && IsTimeProviderType(candidateParams[i].Type, timeProviderSymbol))
            {
                timeProviderIndex = i;
                continue;
            }

            if (originalIndex >= originalParams.Length)
            {
                timeProviderIndex = -1;
                return false;
            }

            if (!SymbolEqualityComparer.Default.Equals(
                    candidateParams[i].Type, originalParams[originalIndex].Type))
            {
                timeProviderIndex = -1;
                return false;
            }

            originalIndex++;
        }

        return timeProviderIndex >= 0 && originalIndex == originalParams.Length;
    }

    private static bool SymbolIsStaticAccessTarget(TargetSymbols symbols, IPropertySymbol propertySymbol)
    {
        if (SymbolEqualityComparer.Default.Equals(propertySymbol.ContainingType, symbols.DateTimeSymbol)
            && TargetPropertyNames.DateTimePropertyNames.Contains(propertySymbol.Name))
        {
            return true;
        }

        if (SymbolEqualityComparer.Default.Equals(propertySymbol.ContainingType, symbols.DateTimeOffsetSymbol)
            && TargetPropertyNames.DateTimeOffsetPropertyNames.Contains(propertySymbol.Name))
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
                properties: ImmutableDictionary<string, string?>.Empty
                    .Add("TimeProviderName", c.TimeProviderName)
                    .Add("PropertyName", c.PropertyName),
                c.TimeProviderName,
                c.PropertyName),
            UseOfStaticTimeAnalyzerContext c => Diagnostic.Create(
                Rules.UseOfStaticTimeDescriptor,
                c.Location,
                properties: ImmutableDictionary<string, string?>.Empty
                    .Add("PropertyName", c.PropertyName),
                c.PropertyName),
            PassTimeProviderAnalyzerContext c => Diagnostic.Create(
                Rules.PassTimeProviderDescriptor,
                c.Location,
                properties: ImmutableDictionary<string, string?>.Empty
                    .Add("TimeProviderName", c.TimeProviderName)
                    .Add("TimeProviderParameterIndex", c.TimeProviderParameterIndex.ToString())
                    .Add("TimeProviderParameterName", c.TimeProviderParameterName),
                c.TimeProviderName,
                c.MethodName),
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
