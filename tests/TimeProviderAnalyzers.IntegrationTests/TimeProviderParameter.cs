using System.Diagnostics;

namespace TimeProviderAnalyzers.IntegrationTests;

public sealed class TimeProviderParameter
{
    public static DateTimeOffset GetDateTimeOffsetNow(TimeProvider timeProvider)
    {
        return DateTimeOffset.Now;
    }

    public static DateTime GetDateTimeNow(TimeProvider timeProvider)
    {
        return DateTime.Now;
    }

    public static DateTimeOffset GetDateTimeOffsetUtcNow(TimeProvider timeProvider)
    {
        return DateTimeOffset.UtcNow;
    }

    public static DateTime GetDateTimeUtcNow(TimeProvider timeProvider)
    {
        return DateTime.UtcNow;
    }
}
