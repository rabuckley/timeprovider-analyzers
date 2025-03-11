namespace TimeProviderAnalyzers.IntegrationTests;

public sealed class TimeProviderInstanceField
{
    private readonly TimeProvider _timeProvider;

    public TimeProviderInstanceField(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public DateTimeOffset GetDateTimeOffsetNow2()
    {
        return _timeProvider.GetUtcNow();
    }

    public DateTimeOffset GetDateTimeOffsetNow()
    {
        return DateTimeOffset.Now;
    }

    public DateTime GetDateTimeNow()
    {
        return DateTime.Now;
    }

    public DateTimeOffset GetDateTimeOffsetUtcNow()
    {
        return DateTimeOffset.UtcNow;
    }

    public DateTime GetDateTimeUtcNow()
    {
        return DateTime.UtcNow;
    }

    public CancellationTokenSource GetCancellationTokenSource()
    {
        return new CancellationTokenSource(12);
    }

    public CancellationTokenSource GetCancellationTokenSourceTimeSpan()
    {
        return new CancellationTokenSource(TimeSpan.Zero);
    }

    public static void Something(Task task)
    {
        task.WaitAsync(TimeSpan.FromSeconds(42));
    }

    public static void Something(Task task, CancellationToken cancellationToken)
    {
        task.WaitAsync(TimeSpan.FromSeconds(42), cancellationToken);
    }

    public static void Something(Task<int> task)
    {
        task.WaitAsync(TimeSpan.FromSeconds(42));
    }

    public static void Something(Task<int> task, CancellationToken cancellationToken)
    {
        task.WaitAsync(TimeSpan.FromSeconds(42), cancellationToken);
    }
}
