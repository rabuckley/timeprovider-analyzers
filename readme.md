# TimeProvider Analyzers

Roslyn analyzers that encourage use of `TimeProvider` over static date/time properties like `DateTime.Now` and `DateTimeOffset.UtcNow`, improving testability in .NET applications.

## Installation

Install the NuGet package as a development dependency:

```shell
dotnet add package TimeProviderAnalyzers
```

## Rules

### `TPA0001`: Use TimeProvider instead of static property *(Info)*

Raised when a static date/time property is used and no `TimeProvider` is in scope. The fix adds a `TimeProvider` parameter to the enclosing method and replaces the static access.

```cs
public void DoSomething()
{
    // TPA0001: Use a method on a TimeProvider instead of the static property 'DateTime.Now'
    var now = DateTime.Now;
}
```

**Fix — adds a `TimeProvider` parameter:**

```cs
public void DoSomething(TimeProvider timeProvider)
{
    var now = timeProvider.GetLocalNow().DateTime;
}
```

---

### `TPA0002`: Use the available TimeProvider instead of a static property *(Warning)*

Raised when a static date/time property is used and a `TimeProvider` is already in scope (as a parameter, local variable, field, or property). The fix replaces the static access with the appropriate `TimeProvider` call.

```cs
public sealed class MyService
{
    private readonly TimeProvider _timeProvider;

    public MyService(TimeProvider timeProvider) => _timeProvider = timeProvider;

    public void DoSomething()
    {
        // TPA0002: Use a method on the available TimeProvider '_timeProvider' instead of the static property 'DateTime.Now'
        var now = DateTime.Now;
    }
}
```

**Fix — uses the available TimeProvider:**

```cs
var now = _timeProvider.GetLocalNow().DateTime;
```

---

### `TPA0003`: Forward the available TimeProvider to methods that accept one *(Warning)*

Raised when a method is called without a `TimeProvider` argument, a `TimeProvider` is in scope, and an overload of that method exists that accepts a `TimeProvider`.

```cs
public void Schedule(TimeProvider timeProvider)
{
    // TPA0003: Pass the available TimeProvider 'timeProvider' to 'Delay'
    Task.Delay(TimeSpan.FromSeconds(1));
}
```

**Fix — passes the available TimeProvider:**

```cs
Task.Delay(TimeSpan.FromSeconds(1), timeProvider);
```

---

## Static property replacements

| Static property | TimeProvider equivalent |
|---|---|
| `DateTime.Now` | `timeProvider.GetLocalNow().DateTime` |
| `DateTime.UtcNow` | `timeProvider.GetUtcNow().UtcDateTime` |
| `DateTime.Today` | `timeProvider.GetLocalNow().Date` |
| `DateTimeOffset.Now` | `timeProvider.GetLocalNow()` |
| `DateTimeOffset.UtcNow` | `timeProvider.GetUtcNow()` |
