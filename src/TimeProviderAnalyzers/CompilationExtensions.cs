using Microsoft.CodeAnalysis;

namespace TimeProviderAnalyzers;

public static class CompilationExtensions
{
    public static bool TryGetTypeByMetadataName(
        this Compilation compilation,
        string metadataName,
        out INamedTypeSymbol? typeSymbol)
    {
        typeSymbol = compilation.GetTypeByMetadataName(metadataName);
        return typeSymbol is not null;
    }

}
