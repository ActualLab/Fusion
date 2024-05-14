using ActualLab.Flows.Internal;

namespace ActualLab.Flows.Infrastructure;

public static class FlowSteps
{
    public static readonly Symbol Start = nameof(Start);
    public static readonly Symbol Error = nameof(Error);
    public static readonly Symbol UnsupportedEvent = nameof(UnsupportedEvent);
    public static readonly Symbol NoStep = nameof(NoStep);

    private static readonly MethodInfo WrappedSystemStepMethod = typeof(FlowSteps)
        .GetMethod(nameof(WrappedSystemStep), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo WrappedStepMethod = typeof(FlowSteps)
        .GetMethod(nameof(WrappedStep), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly ConcurrentDictionary<(Type, Symbol), Func<Flow, object?, CancellationToken, Task>>
        Cache = new();

    public static Task Invoke(Flow flow, Symbol stepName, object? @event, CancellationToken cancellationToken)
        => Get(flow.GetType(), stepName).Invoke(flow, @event, cancellationToken);

    public static Func<Flow, object?, CancellationToken, Task> Get(Type flowType, Symbol stepName)
        => Cache.GetOrAdd((flowType, stepName), static key => {
            var (type, step) = key;
            if (type.IsGenericTypeDefinition)
                throw new ArgumentOutOfRangeException(nameof(flowType));

            step = step.Or("Start");
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)) {
                if (!Equals(method.Name, step.Value))
                    continue;
                if (method.ReturnType != typeof(Task))
                    continue;

                var parameters = method.GetParameters();
                if (parameters.Length != 2)
                    continue;
                if (parameters[^1].ParameterType != typeof(CancellationToken))
                    continue;

                var eventType = parameters[0].ParameterType;
                var delegateType = typeof(Func<,,,>)
                    .MakeGenericType(
                        type,
                        eventType,
                        typeof(CancellationToken),
                        typeof(Task));
                var stepImpl = method.CreateDelegate(delegateType);
                var isSystem = step == Error || step == UnsupportedEvent || step == NoStep;
                var wrappedStepMethod = isSystem
                    ? WrappedSystemStepMethod
                    : WrappedStepMethod;
                var result = (Func<Flow, object?, CancellationToken, Task>)wrappedStepMethod
                    .MakeGenericMethod(type, eventType)
                    .Invoke(null, [stepImpl])!;
                return result;
            }
            return step == NoStep
                ? throw Errors.NoStep(type, step.Value)
                : Get(type, NoStep);
        });

    private static Func<Flow, object?, CancellationToken, Task> WrappedSystemStep<TFlow, TEvent>(Delegate stepImpl)
        where TFlow : Flow
        where TEvent : class
        => (untypedFlow, untypedEvent, cancellationToken) => {
            var fn = (Func<TFlow, TEvent?, CancellationToken, Task>)stepImpl;
            return fn.Invoke((TFlow)untypedFlow, (TEvent?)untypedEvent, cancellationToken);
        };

    private static Func<Flow, object?, CancellationToken, Task> WrappedStep<TFlow, TEvent>(Delegate stepImpl)
        where TFlow : Flow
        where TEvent : class
        => (untypedFlow, untypedEvent, cancellationToken) => {
            var fn = untypedEvent switch {
                TEvent => (Func<TFlow, TEvent?, CancellationToken, Task>)stepImpl,
                _ => Get(typeof(TFlow), UnsupportedEvent),
            };
            return fn.Invoke((TFlow)untypedFlow, (TEvent?)untypedEvent, cancellationToken);
        };
}
