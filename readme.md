# TimeProvider Analyzers

A set of .NET analyzers enforcing usage of `TimeProvider`.

## Rules

### `TPA0001`: Use TimeProvider instead of static property to access date and time information

Defaults to Information severity.

```cs
public void DoSomething()
{
    var now = DateTime.Now; // Use a method on a TimeProvider instead of the static property 'DateTime.Now' to access date and time information.(TPA0001)
}
```

### `TPA0002`: Use the available TimeProvider instead of a static property to access date and time information

Defaults to Warning severity.

```cs
public sealed class MyService(TimeProvider timeProvider)
{
    private readonly TimeProvider _timeProvider = timeProvider;

    public void DoSomething()
    {
        var now = DateTime.Now; // Use a method on the available TimeProvider '_timeProvider' instead of the static property 'DateTime.Now' to access date and time information.(TPA0002)
    }
}
```
