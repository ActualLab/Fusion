# Operations Framework: Reprocessing

**Operation Reprocessing** automatically retries commands that fail with transient errors.
This is essential for handling temporary failures like:
- Database connection issues
- Deadlocks
- Network timeouts
- Concurrent modification conflicts


## Enabling Reprocessing

```cs
var fusion = services.AddFusion();
fusion.AddOperationReprocessor();  // Enable operation reprocessing
```


## How It Works

1. Command execution starts
2. If a transient error occurs, `OperationReprocessor` catches it
3. Checks if retry is allowed (filter, retry count, error type)
4. If allowed, waits for delay and retries the command
5. Repeats until success or max retries exceeded

```
Command Execution
       |
       v
  +---------+     Success
  | Execute |---------------------> Done
  +---------+
       |
       | Transient Error
       v
  +--------------+     Not Retryable
  | Can Retry?   |---------------------> Throw
  +--------------+
       |
       | Yes
       v
  +--------------+
  | Wait Delay   |
  +--------------+
       |
       +----------------------------> Retry
```


## Configuration Options

```cs
fusion.AddOperationReprocessor(_ => new() {
    MaxRetryCount = 3,  // Default: 3
    RetryDelays = RetryDelaySeq.Exp(0.50, 3, 0.33),  // Exponential backoff
    Filter = (command, context) => /* custom filter */,
});
```

### Options Reference

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `MaxRetryCount` | `int` | 3 | Maximum retry attempts |
| `RetryDelays` | `RetryDelaySeq` | Exp(0.50, 3, 0.33) | Delay sequence between retries |
| `DelayClock` | `MomentClock?` | `null` | Custom clock for delays |
| `Filter` | `Func<ICommand, CommandContext, bool>` | `DefaultFilter` | Custom retry filter |


## Retry Delays

The default retry delays use exponential backoff:

```cs
RetryDelaySeq.Exp(0.50, 3, 0.33)
// Base: 0.5 seconds
// Max multiplier: 3x
// Jitter: ±33%

// Produces delays approximately:
// Retry 1: ~0.5s (0.33s - 0.67s)
// Retry 2: ~1.65s (1.1s - 2.2s)
// Retry 3: ~5.45s (3.6s - 7.3s)
```


## Default Filter

The default filter determines which commands can be retried:

```cs
public static bool DefaultFilter(ICommand command, CommandContext context)
{
    // Only on server
    if (!RuntimeInfo.IsServer)
        return false;

    // Skip delegating commands (proxies)
    if (command is IDelegatingCommand)
        return false;

    // Skip scoped Commander commands (UI commands)
    if (context.Commander.Hub.IsScoped)
        return false;

    // Only root-level commands
    return true;
}
```


## Filtering Conditions

A command is eligible for reprocessing when **all** of these are true:

1. The error is classified as transient
2. `Filter` function returns `true`
3. Retry count hasn't exceeded `MaxRetryCount`
4. No existing `Operation` has started
5. No `Invalidation` is active
6. Not a nested command
7. Not an `ISystemCommand`


## Transient Error Detection

The reprocessor uses `TransiencyResolver<IOperationReprocessor>` to classify errors:

| Error Type | Transiency |
|------------|------------|
| `ITransientException` | Transient |
| `DbUpdateConcurrencyException` | Transient |
| `SocketException` | Transient |
| `TimeoutException` | Transient |
| Other exceptions | Non-transient (no retry) |


## Super-Transient Errors

Some errors are classified as **super-transient**, meaning they can retry indefinitely
(ignoring `MaxRetryCount`):

```cs
// Super-transient errors retry without limit
if (transiency == Transiency.SuperTransient)
    return true;  // Always retry
```

This is useful for errors like:
- Connection pool exhausted (will resolve when connections free up)
- Service temporarily unavailable


## Custom Transiency Detection

Register custom error classifiers:

```cs
services.AddSingleton<ITransiencyResolver<IOperationReprocessor>, MyTransiencyResolver>();

public class MyTransiencyResolver : ITransiencyResolver<IOperationReprocessor>
{
    public Transiency GetTransiency(Exception error)
    {
        if (error is MyCustomRetryableException)
            return Transiency.Transient;

        if (error is MyRateLimitException)
            return Transiency.SuperTransient;  // Retry indefinitely

        return Transiency.Unknown;  // Fall through to other resolvers
    }
}
```


## Context Reset on Retry

When a command is retried, the execution context is reset:

- New `CommandContext` created
- Previous `Operation` discarded
- `Items` collections cleared
- New execution ID assigned

This ensures each retry attempt starts fresh.


## Logging

Reprocessing logs warnings for retries:

```
[WRN] OperationReprocessor: Reprocessing MyCommand after 500ms delay (attempt 1/3)
```


## Best Practices

1. **Keep commands idempotent** &ndash; Retries may execute the same logic multiple times
2. **Use appropriate retry counts** &ndash; Too many retries delay failure reporting
3. **Consider error types** &ndash; Not all errors should be retried
4. **Log retry context** &ndash; Include relevant information for debugging
5. **Set reasonable delays** &ndash; Give systems time to recover


## Example: Custom Retry Policy

```cs
fusion.AddOperationReprocessor(_ => new() {
    MaxRetryCount = 5,
    RetryDelays = RetryDelaySeq.Exp(
        TimeSpan.FromSeconds(1),     // Base delay
        TimeSpan.FromSeconds(30)),   // Max delay
    Filter = (command, context) => {
        // Don't retry admin commands
        if (command is IAdminCommand)
            return false;

        // Don't retry commands from specific users
        if (command is IUserCommand userCmd && userCmd.UserId == SpecialUserId)
            return false;

        return OperationReprocessor.DefaultFilter(command, context);
    },
});
```
