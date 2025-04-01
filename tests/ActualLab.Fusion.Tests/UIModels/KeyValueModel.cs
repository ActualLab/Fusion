using ActualLab.Fusion.Tests.Services;
using ActualLab.Fusion.UI;

namespace ActualLab.Fusion.Tests.UIModels;

public class KeyValueModel<TValue>
{
    public string Key { get; set; } = "";
    public TValue Value { get; set; } = default!;
    public int UpdateCount { get; set; }
}

public class StringKeyValueModelState : ComputedState<KeyValueModel<string>>
{
    private MutableState<string> Locals { get; }

    private IKeyValueService<string> KeyValueService
        => Services.GetRequiredService<IKeyValueService<string>>();

    public StringKeyValueModelState(IServiceProvider services)
        : base(new(), services, false)
    {
        Locals = services.StateFactory().NewMutable("");
        Locals.AddEventHandler(StateEventKind.Updated, (_, _) => _ = this.Recompute());

        // ReSharper disable once VirtualMemberCallInConstructor
        Initialize(new Options() {
            UpdateDelayer = new UpdateDelayer(services.UIActionTracker(), 0.5),
            InitialValue = null!,
        });
    }

    protected override Task Compute(CancellationToken cancellationToken)
    {
        return Implementation(cancellationToken);

        async Task<KeyValueModel<string>> Implementation(CancellationToken cancellationToken)
        {
            if (IsDisposed) // Never complete if the state is already disposed
                await TaskExt.NewNeverEndingUnreferenced().WaitAsync(cancellationToken).ConfigureAwait(false);

            var updateCount = ValueOrDefault?.UpdateCount ?? 0;
            var key = Locals.ValueOrDefault ?? "";
            var value = await KeyValueService.Get(key, cancellationToken).ConfigureAwait(false);
            return new KeyValueModel<string>() {
                Key = key,
                Value = value,
                UpdateCount = updateCount + 1,
            };
        }
    }
}
