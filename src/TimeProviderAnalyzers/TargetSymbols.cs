using Microsoft.CodeAnalysis;

namespace TimeProviderAnalyzers;

public sealed record TargetSymbols
{
    public required INamedTypeSymbol TimeProviderSymbol { get; init; }

    public required INamedTypeSymbol DateTimeOffsetSymbol { get; init; }

    public required INamedTypeSymbol DateTimeSymbol { get; init; }
}
