using Microsoft.CodeAnalysis;

namespace TimeProviderAnalyzers;

public static class PropertySymbolExtensions
{
    public static string GetContainingTypeQualifiedPropertyName(this IPropertySymbol propertySymbol)
    {
        return $"{propertySymbol.ContainingType.Name}.{propertySymbol.Name}";
    }
}
