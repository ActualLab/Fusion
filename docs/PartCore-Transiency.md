# Transiency Resolvers

`Transiency` classifies exceptions as transient (retryable) or terminal (non-retryable),
enabling intelligent retry policies throughout Fusion.

## Key Types

| Type | Description | Source |
|------|-------------|--------|
| `Transiency` | Exception classification result | [Transiency.cs](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Core/Resilience/Transiency.cs) |
| `TransiencyResolver` | Determines if exception is transient | [TransiencyResolver.cs](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Core/Resilience/TransiencyResolver.cs) |
| `TransiencyResolvers` | Built-in resolver implementations | [TransiencyResolvers.cs](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Core/Resilience/TransiencyResolvers.cs) |
| `RetryPolicy` | Configurable retry with backoff | [RetryPolicy.cs](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Core/Resilience/RetryPolicy.cs) |


## Why Transiency?

Not all errors should trigger retries:

| Error Type | Should Retry? | Example |
|------------|---------------|---------|
| Network timeout | Yes | `TimeoutException` |
| Connection reset | Yes | `SocketException` |
| Server overloaded | Yes | HTTP 503 |
| Invalid argument | No | `ArgumentException` |
| Not found | No | HTTP 404 |
| Authentication failed | No | HTTP 401 |

`TransiencyResolver` makes this classification automatic and consistent.


## Transiency Enum

```cs
public enum Transiency
{
    Unknown = 0,      // Not determined
    Terminal = 1,     // Don't retry
    Transient = 2,    // Safe to retry
    SuperTransient = 3 // Retry immediately (no delay)
}
```

| Value | Meaning | Typical Action |
|-------|---------|----------------|
| `Unknown` | Classification failed | Treat as terminal |
| `Terminal` | Permanent error | Don't retry, report error |
| `Transient` | Temporary error | Retry with backoff |
| `SuperTransient` | Very temporary | Retry immediately |


## TransiencyResolver

### Basic Usage

```cs
var resolver = TransiencyResolver.New(services);

try {
    await DoOperation();
}
catch (Exception ex) {
    var transiency = resolver.Invoke(ex);

    if (transiency.IsTransient()) {
        // Retry the operation
        await Task.Delay(backoff);
        await DoOperation();
    }
    else {
        // Report permanent failure
        throw;
    }
}
```

### Default Resolver

The default resolver handles common transient exceptions:

```cs
// Built-in transient exceptions:
// - OperationCanceledException (SuperTransient)
// - TimeoutException
// - SocketException
// - HttpRequestException (certain status codes)
// - DbException (certain error codes)

var resolver = TransiencyResolver.New(services);
```

### Custom Resolver

```cs
// Add custom logic
var customResolver = TransiencyResolver.New(
    TransiencyResolvers.PreferTransient,  // Base behavior
    ex => ex switch {
        MyCustomException e => e.IsRetryable
            ? Transiency.Transient
            : Transiency.Terminal,
        _ => Transiency.Unknown  // Let other resolvers decide
    }
);
```

### Chaining Resolvers

Resolvers can be chained — first non-`Unknown` result wins:

```cs
var resolver = TransiencyResolver.New(
    // Check custom exceptions first
    ex => ex is MyException me ? me.Transiency : Transiency.Unknown,
    // Then fall back to defaults
    TransiencyResolvers.PreferTransient
);
```


## Built-in Resolvers

### TransiencyResolvers.PreferTransient

Assumes unknown exceptions are transient:

```cs
var resolver = TransiencyResolvers.PreferTransient;
// Unknown → Transient
```

### TransiencyResolvers.PreferTerminal

Assumes unknown exceptions are terminal:

```cs
var resolver = TransiencyResolvers.PreferTerminal;
// Unknown → Terminal
```


## RetryPolicy

Combines `TransiencyResolver` with retry logic:

```cs
var policy = new RetryPolicy(
    maxRetryCount: 5,
    retryDelays: RetryDelaySeq.Exp(1, 60),  // 1s → 60s exponential
    transiencyResolver: TransiencyResolver.New(services)
);

// Execute with retry
await policy.Run(async ct => {
    await DoOperationAsync(ct);
}, cancellationToken);
```

### Retry Delay Sequences

```cs
// Exponential backoff: 1s, 2s, 4s, 8s, ... up to 60s
var exp = RetryDelaySeq.Exp(
    TimeSpan.FromSeconds(1),
    TimeSpan.FromSeconds(60)
);

// Fixed delay: always 5s
var fixed = RetryDelaySeq.Fixed(TimeSpan.FromSeconds(5));

// Linear: 1s, 2s, 3s, 4s, ... up to 30s
var linear = RetryDelaySeq.Linear(
    TimeSpan.FromSeconds(1),
    TimeSpan.FromSeconds(30)
);
```


## Usage in Fusion

### Operations Framework Reprocessing

The Operations Framework uses transiency to determine if failed operations should be reprocessed:

```cs
// In operation reprocessing
catch (Exception ex) {
    var transiency = TransiencyResolver.Invoke(ex);
    if (transiency.IsTransient()) {
        // Mark for reprocessing
        await ScheduleReprocess(operation);
    }
    else {
        // Log permanent failure
        Log.Error(ex, "Operation failed permanently");
    }
}
```

See [Operations Framework Reprocessing](./PartO-RP.md) for details.

### RPC Connection Handling

RPC uses transiency for reconnection decisions:

```cs
// Connection error handling
catch (Exception ex) {
    var transiency = TransiencyResolver.Invoke(ex);
    if (transiency.IsTransient()) {
        // Attempt reconnection with backoff
        await ReconnectWithBackoff();
    }
    else {
        // Connection permanently failed
        await NotifyConnectionLost();
    }
}
```

### DbEntityResolver

Database entity resolvers use transiency for query retry:

```cs
// Retry transient database errors
var policy = new RetryPolicy(3, RetryDelaySeq.Exp(0.1, 5));
return await policy.Run(async ct => {
    await using var db = await DbHub.CreateDbContext(ct);
    return await db.Entities.FindAsync(key, ct);
}, cancellationToken);
```


## Common Patterns

### Service Method with Retry

```cs
public async Task<Data> FetchDataAsync(CancellationToken ct)
{
    var policy = new RetryPolicy(
        maxRetryCount: 3,
        retryDelays: RetryDelaySeq.Exp(0.5, 10),
        transiencyResolver: _transiencyResolver
    );

    return await policy.Run(async innerCt => {
        return await _httpClient.GetFromJsonAsync<Data>(url, innerCt);
    }, ct);
}
```

### Custom Exception Classification

```cs
// Mark your exceptions as transient/terminal
public class RateLimitedException : Exception
{
    public TimeSpan RetryAfter { get; }
}

// Register resolver
services.AddSingleton<TransiencyResolver>(sp =>
    TransiencyResolver.New(
        ex => ex switch {
            RateLimitedException => Transiency.Transient,
            AuthenticationException => Transiency.Terminal,
            _ => Transiency.Unknown
        },
        TransiencyResolvers.PreferTransient
    )
);
```

### Logging Transient vs Terminal Errors

```cs
catch (Exception ex) {
    var transiency = _transiencyResolver.Invoke(ex);

    if (transiency.IsTransient()) {
        _log.LogWarning(ex, "Transient error, will retry");
    }
    else {
        _log.LogError(ex, "Permanent error");
        throw;
    }
}
```


## Best Practices

### Default to Terminal for Unknown

```cs
// Good: Safer default
var resolver = TransiencyResolvers.PreferTerminal;

// Risky: May retry non-retryable operations
var resolver = TransiencyResolvers.PreferTransient;
```

### Classify Your Exceptions

```cs
// Good: Explicit classification
public class MyServiceException : Exception
{
    public bool IsTransient { get; init; }
}

// Register resolver
TransiencyResolver.New(ex =>
    ex is MyServiceException mse
        ? mse.IsTransient ? Transiency.Transient : Transiency.Terminal
        : Transiency.Unknown
);
```

### Use Appropriate Backoff

```cs
// Good: Exponential backoff with reasonable bounds
RetryDelaySeq.Exp(
    TimeSpan.FromMilliseconds(100),  // Start small
    TimeSpan.FromSeconds(30)          // Don't wait too long
)

// Bad: Immediate retry (may overwhelm failing service)
RetryDelaySeq.Fixed(TimeSpan.Zero)
```
