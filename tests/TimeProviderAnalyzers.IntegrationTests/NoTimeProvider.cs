namespace TimeProviderAnalyzers.IntegrationTests;

public sealed class NoTimeProvider
{ 
    public static DateTimeOffset GetDateTimeOffsetNow()
    {
        return DateTimeOffset.Now;
    }

    public static DateTime GetDateTimeNow()
    {
        return DateTime.Now;
    }

    public static DateTimeOffset GetDateTimeOffsetUtcNow()
    {
        return DateTimeOffset.UtcNow;
    }

    public static DateTime GetDateTimeUtcNow()
    {
        return DateTime.UtcNow;
    }
}
