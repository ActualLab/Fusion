using Microsoft.AspNetCore.Components.Authorization;
using ActualLab.Fusion.UI;
using Samples.TodoApp.Abstractions;

namespace Samples.TodoApp.UI.Services;

/// <summary>
/// Blazor <see cref="AuthenticationStateProvider"/> backed by <see cref="IUserApi"/>
/// that automatically updates when the user's authentication state changes.
/// </summary>
public sealed class AuthStateProvider : AuthenticationStateProvider, IDisposable
{
    /// <summary>
    /// Configuration options for <see cref="AuthStateProvider"/>.
    /// </summary>
    public record Options
    {
        public static Options Default { get; set; } = new();

        public IUpdateDelayer UpdateDelayer { get; init; }

        public Options()
        {
            var updateDelayer = FixedDelayer.NextTick;
            UpdateDelayer = updateDelayer with {
                RetryDelays = updateDelayer.RetryDelays with { Max = TimeSpan.FromSeconds(10) },
            };
        }
    };

#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private Session? _session;
    private volatile Task<AuthState> _authStateTask;
    private volatile Task<AuthenticationState> _authenticationStateTask;

    private IServiceProvider Services { get; }
    private IUserApi UserApi { get; }
    private ISessionResolver SessionResolver { get; }
    private UIActionTracker UIActionTracker { get; }

    public ComputedState<AuthState> ComputedState { get; }

    public AuthStateProvider(Options settings, IServiceProvider services)
    {
        Services = services;
        SessionResolver = services.GetRequiredService<ISessionResolver>();
        UserApi = services.GetRequiredService<IUserApi>();
        UIActionTracker = services.UIActionTracker();

        var initialAuthState = new AuthState();
        lock (_lock) {
            _authStateTask = Task.FromResult(initialAuthState);
            _authenticationStateTask = Task.FromResult((AuthenticationState)initialAuthState);
        }
        var stateOptions = new ComputedState<AuthState>.Options() {
            InitialValue = initialAuthState,
            UpdateDelayer = settings.UpdateDelayer,
            EventConfigurator = state => state.AddEventHandler(StateEventKind.Updated, OnStateChanged),
            FlowExecutionContext = true,
        };
        ComputedState = services.StateFactory().NewComputed(stateOptions, ComputeState);
    }

    public void Dispose()
        => ComputedState.Dispose();

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => _authenticationStateTask;

    private async Task<AuthState> ComputeState(CancellationToken cancellationToken)
    {
        _session ??= await SessionResolver.GetSession(cancellationToken).ConfigureAwait(false);
        var user = await UserApi.GetOwn(_session, cancellationToken).ConfigureAwait(false);
        // For now, we don't separately check IsSignOutForced for guests.
        // The original code checks Auth.IsSignOutForced for null users.
        // We simplify: if user is null, we don't have forced sign-out info.
        return new AuthState(user, false);
    }

    private void OnStateChanged(State state, StateEventKind eventKind)
        => _ = Task.Run(() => {
            var typedState = (IState<AuthState>)state;
            Task<AuthState> authStateTask;
            Task<AuthenticationState> authenticationStateTask;
            lock (_lock) {
                var authState = typedState.LastNonErrorValue;
                var oldAuthState = _authStateTask.GetAwaiter().GetResult();
                if (authState.IsIdenticalTo(oldAuthState))
                    return;

                authStateTask = _authStateTask = Task.FromResult(authState);
                authenticationStateTask = _authenticationStateTask = Task.FromResult((AuthenticationState)authState);
            }

            NotifyAuthenticationStateChanged(authenticationStateTask);
            var clock = UIActionTracker.Clock;
            var action = new UIAction<AuthState>(new ChangeAuthStateUICommand(), clock, authStateTask, default);
            UIActionTracker.Register(action);
        });
}
