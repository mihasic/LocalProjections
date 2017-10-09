namespace LocalProjections
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class RecoverableSubscriptionAdapter : IDisposable
    {
        private class State : IDisposable
        {
            private readonly object _sync = new object();
            private Action _dispose;

            public State(Action dispose = null)
            {
                _dispose = dispose;
            }

            public void SetState(Action dispose)
            {
                lock (_sync)
                {
                    _dispose = dispose;
                }
            }

            public void Dispose()
            {
                lock (_sync)
                {
                    _dispose?.Invoke();
                    _dispose = null;
                }
            }
        }
        
        private readonly CreateSubscription _createSubscription;
        private readonly Func<Task<IStatefulProjection>> _createProjection;
        private readonly Func<AllStreamPosition> _getStartingPosition;
        private readonly CancellationTokenSource _stopSource = new CancellationTokenSource();
        private readonly State _state = new State();
        private int _started = 0;

        public RecoverableSubscriptionAdapter(
            CreateSubscription createSubscription,
            Func<Task<IStatefulProjection>> createProjection,
            Func<AllStreamPosition> getStartingPosition)
        {
            _createSubscription = createSubscription;
            _createProjection = createProjection;
            _getStartingPosition = getStartingPosition;
        }

        public void Start()
        {
            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0) return;

            _stopSource.Token.Register(_state.Dispose);

            Task.Run(() => Run(_stopSource.Token));
        }

        private async Task Run(CancellationToken cancellationToken)
        {
            _state.Dispose();

            if (cancellationToken.IsCancellationRequested)
                return;

            var host = await _createProjection();

            var subscr = await _createSubscription(
                _getStartingPosition(),
                (s, m, ct) => host.Project(m, ct),
                () => host.Commit(cancellationToken),
                ex =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                        Task.Run(() => Run(cancellationToken));

                    return Task.CompletedTask;
                }).ConfigureAwait(false);

            _state.SetState(() =>
            {
                subscr.Dispose();
                host.Dispose();
            });
        }

        public void Restart()
        {
            _state.Dispose();
            var token = _stopSource.Token;
            if (!token.IsCancellationRequested)
                Task.Run(() => Run(token));
        }

        public void Dispose() =>
            _stopSource.Cancel();
    }
}
