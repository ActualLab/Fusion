using Microsoft.AspNetCore.Components.Authorization;
using ActualLab.Fusion.Authentication;
using ActualLab.Fusion.UI;

namespace ActualLab.Fusion.Blazor.Authentication;

public sealed class AuthStateProvider : AuthenticationStateProvider, IDisposable
{
    public record Options
    {
        public static Options Default { get; set; } = new();

        public IUpdateDelayer UpdateDelayer { get; init; }

        public Options()
        {
            var updateDelayer = FixedDelayer.NextTick; // Must be small, otherwise the auth delay will be percievable
            UpdateDelayer = updateDelayer with {
                RetryDelays = updateDelayer.RetryDelays with { Max = TimeSpan.FromSeconds(10) },
            };
        }
    };

    private readonly object _lock = new();
    private Session? _session;
    private volatile Task<AuthState> _authStateTask;
    private volatile Task<AuthenticationState> _authenticationStateTask;

    private IServiceProvider Services { get; }
    private IAuth Auth { get; }
    private ISessionResolver SessionResolver { get; }
    private UIActionTracker UIActionTracker { get; }

    public ComputedState<AuthState> ComputedState { get; }

    public AuthStateProvider(Options settings, IServiceProvider services)
    {
        Services = services;
        SessionResolver = services.GetRequiredService<ISessionResolver>();
        Auth = services.GetRequiredService<IAuth>();
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
            FlowExecutionContext = true, // To preserve current culture
        };
        ComputedState = services.StateFactory().NewComputed(stateOptions, ComputeState);
    }

    public void Dispose()
        => ComputedState.Dispose();

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => _authenticationStateTask;

    private async Task<AuthState> ComputeState(CancellationToken cancellationToken)
    {
        // We have to use ISessionResolver.GetSession() here
        _session ??= await SessionResolver.GetSession(cancellationToken).ConfigureAwait(false);
        var user = await Auth.GetUser(_session, cancellationToken).ConfigureAwait(false);
        // AuthService.GetUser checks for forced sign-out as well, so
        // we should explicitly query its state for unauthenticated users only
        var isSignOutForced = user == null
            && await Auth.IsSignOutForced(_session, cancellationToken).ConfigureAwait(false);
        return new AuthState(user, isSignOutForced);
    }

    private void OnStateChanged(IState<AuthState> state, StateEventKind eventKind)
        => _ = Task.Run(() => {
            Task<AuthState> authStateTask;
            Task<AuthenticationState> authenticationStateTask;
            lock (_lock) {
                var authState = state.LastNonErrorValue;
                var oldAuthState = _authStateTask.Result;
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
