using System.Collections.Generic;

namespace TimeProviderAnalyzers;

internal static class TargetPropertyNames
{
    private const string Now = "Now";
    private const string UtcNow = "UtcNow";
    private const string Today = "Today";

    public static readonly IReadOnlyCollection<string> DateTimePropertyNames = [Now, UtcNow, Today];

    public static readonly IReadOnlyCollection<string> DateTimeOffsetPropertyNames = [Now, UtcNow];
}
