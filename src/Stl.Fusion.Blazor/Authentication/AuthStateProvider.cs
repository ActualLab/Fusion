using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;
using Stl.Fusion.Authentication;

namespace Stl.Fusion.Blazor
{
    public class AuthStateProvider : AuthenticationStateProvider, IDisposable
    {
        public class Options
        {
            public Action<LiveState<AuthState>.Options> LiveStateOptionsBuilder { get; } =
                DefaultLiveStateOptionsBuilder;

            public static void DefaultLiveStateOptionsBuilder(LiveState<AuthState>.Options options)
                => options.WithUpdateDelayer(0.1, 10);
        }

        // These properties are intentionally public -
        // e.g. State is quite handy to consume in other compute methods or states
        public ISessionResolver SessionResolver { get; }
        public IAuthService AuthService { get; }
        public ILiveState<AuthState> State { get; }

        public AuthStateProvider(
            Options? options,
            IAuthService authService,
            ISessionResolver sessionResolver,
            IStateFactory stateFactory)
        {
            options ??= new();
            AuthService = authService;
            SessionResolver = sessionResolver;
            State = stateFactory.NewLive<AuthState>(o => {
                options.LiveStateOptionsBuilder.Invoke(o);
                o.InitialOutputFactory = _ => new AuthState(new User(""));
                o.EventConfigurator += state => state.AddEventHandler(StateEventKind.All, OnStateChanged);
            }, ComputeState);
        }

        public void Dispose() => State.Dispose();
        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var state = await State.UpdateAsync(false).ConfigureAwait(false);
            return state.LastValue;
        }

        protected virtual async Task<AuthState> ComputeState(ILiveState<AuthState> state, CancellationToken cancellationToken)
        {
            var session = await SessionResolver.GetSessionAsync(cancellationToken).ConfigureAwait(false);
            var user = await AuthService.GetUserAsync(session, cancellationToken).ConfigureAwait(false);
            // AuthService.GetUserAsync checks for forced sign-out as well, so
            // we should explicitly query its state for unauthenticated users only
            var isSignOutForced = !user.IsAuthenticated
                && await AuthService.IsSignOutForcedAsync(session, cancellationToken).ConfigureAwait(false);
            return new AuthState(user, isSignOutForced);
        }

        protected virtual void OnStateChanged(IState<AuthState> state, StateEventKind eventKind)
        {
            if (eventKind == StateEventKind.Updated)
                NotifyAuthenticationStateChanged(Task.FromResult((AuthenticationState) state.LastValue));
        }
    }
}
