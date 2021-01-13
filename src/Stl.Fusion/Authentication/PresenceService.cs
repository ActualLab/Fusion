using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Stl.Async;

namespace Stl.Fusion.Authentication
{
    public class PresenceService : AsyncProcessBase
    {
        public class Options
        {
            public TimeSpan UpdatePeriod { get; set; } = TimeSpan.FromMinutes(10);
        }

        protected ILogger Log { get; }
        protected IAuthService AuthService { get; }
        protected ISessionResolver SessionResolver { get; }
        protected IUpdateDelayer UpdateDelayer { get; }
        protected TimeSpan UpdatePeriod { get; }

        public PresenceService(
            Options? options,
            IAuthService authService,
            ISessionResolver sessionResolver,
            ILogger<PresenceService>? log = null)
        {
            options ??= new();
            Log = log ?? NullLogger<PresenceService>.Instance;
            AuthService = authService;
            SessionResolver = sessionResolver;
            UpdateDelayer = new UpdateDelayer(new UpdateDelayer.Options() {
                Delay = options.UpdatePeriod,
                CancellationDelay = TimeSpan.Zero,
            });
        }

        public virtual void UpdatePresence() => UpdateDelayer.CancelDelays();

        protected override async Task RunInternalAsync(CancellationToken cancellationToken)
        {
            var session = await SessionResolver.GetSessionAsync(cancellationToken).ConfigureAwait(false);
            var retryCount = 0;
            while (!cancellationToken.IsCancellationRequested) {
                await UpdateDelayer.DelayAsync(retryCount, cancellationToken).ConfigureAwait(false);
                var success = await UpdatePresenceAsync(session, cancellationToken).ConfigureAwait(false);
                retryCount = success ? 0 : 1 + retryCount;
            }
        }

        protected virtual async Task<bool> UpdatePresenceAsync(Session session, CancellationToken cancellationToken)
        {
            try {
                await AuthService.UpdatePresenceAsync(session, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception e) {
                Log.LogError(e, "Error on UpdatePresenceAsync call.");
                return false;
            }
        }
    }
}
