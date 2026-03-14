using System.Collections.Immutable;

namespace TimeProviderAnalyzers;

/// <summary>
/// Maps "Type.Property" to the TimeProvider replacement expression template.
/// {0} is replaced with the TimeProvider variable name at the call site.
/// </summary>
internal static class TimeProviderReplacements
{
    public static readonly ImmutableDictionary<string, string> Expressions =
        ImmutableDictionary<string, string>.Empty
            .Add("DateTime.Now", "{0}.GetLocalNow().DateTime")
            .Add("DateTime.UtcNow", "{0}.GetUtcNow().DateTime")
            .Add("DateTime.Today", "{0}.GetLocalNow().Date")
            .Add("DateTimeOffset.Now", "{0}.GetLocalNow()")
            .Add("DateTimeOffset.UtcNow", "{0}.GetUtcNow()");
}
