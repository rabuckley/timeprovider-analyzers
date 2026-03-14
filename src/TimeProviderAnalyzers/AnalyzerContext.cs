using Microsoft.CodeAnalysis;

namespace TimeProviderAnalyzers;

internal abstract record AnalyzerContext
{
    public required Location Location { get; init; }
}

internal record UseOfStaticTimeAnalyzerContext : AnalyzerContext
{
    public required string PropertyName { get; init; }
}

internal sealed record UseOfStaticTimeWithTimeProviderInScopeAnalyzerContext : UseOfStaticTimeAnalyzerContext
{
    public required string TimeProviderName { get; init; }
}

internal sealed record PassTimeProviderAnalyzerContext : AnalyzerContext
{
    public required string TimeProviderName { get; init; }
    public required string MethodName { get; init; }
    public required int TimeProviderParameterIndex { get; init; }
    public required string TimeProviderParameterName { get; init; }
}
