namespace LocalProjections
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using LightningStore;

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
        private readonly IReadOnlyDictionary<string, Func<IStatefulProjection>> _projectionGroups;
        private readonly string _checkpointsDir;

        private readonly ProjectionGroupStateObserver _observer = new ProjectionGroupStateObserver();
        private readonly ConcurrentDictionary<string, CheckpointStore> _checkpointStores =
            new ConcurrentDictionary<string, CheckpointStore>();
        private readonly CancellationTokenSource _stopSource = new CancellationTokenSource();
        private readonly State _state = new State();

        private int _started = 0;

        public RecoverableSubscriptionAdapter(
            CreateSubscription createSubscription,
            string baseDir,
            IReadOnlyDictionary<string, Func<IStatefulProjection>> projectionGroups)
        {
            _checkpointsDir = Path.Combine(baseDir, "checkpoints");
            Directory.CreateDirectory(_checkpointsDir);
            _createSubscription = createSubscription;
            _projectionGroups = projectionGroups;
        }

        public ProjectionGroupStateObserver ProjectionGroupState => _observer;

        private IStatefulProjection WrapProjection(
            string name,
            IStatefulProjection projection,
            int maxBatchSize = 2048)
        {
            var checkpointStore = _checkpointStores.GetOrAdd(name,
                _ => new CheckpointStore(Path.Combine(_checkpointsDir, $"{name}.cpt")));
            return new SuspendableProjection(
                new CommitNthProjection(
                    new CheckpointProjection(
                        projection,
                        checkpointStore,
                        cp => _observer[name] = _observer[name].MoveTo(cp)
                    ),
                    maxBatchSize
                ),
                ex => _observer[name] = _observer[name].Suspend(ex)
            );
        }

        public void Start()
        {
            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0) return;

            _stopSource.Token.Register(() =>
            {
                _state.Dispose();
                foreach (var cpStore in _checkpointStores.Values)
                    cpStore.Dispose();
                _checkpointStores.Clear();
            });

            Task.Run(() => Run(_stopSource.Token));
        }

        private async Task Run(CancellationToken cancellationToken)
        {
            _state.Dispose();

            if (cancellationToken.IsCancellationRequested)
                return;

            var projectionGroups = _projectionGroups
                .ToDictionary(x => x.Key, x => WrapProjection(x.Key, x.Value()));

            Func<IReadOnlyCollection<IStatefulProjection>> filtered =
                () => _observer.Active.Select(x => projectionGroups[x]).ToArray();

            var host = new ParallelExecutionHost(filtered);

            var subscr = await _createSubscription(
                _observer.Min,
                (s, m, ct) => host.Project(m, ct),
                () => host.Commit(),
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
                foreach (var p in projectionGroups.Values)
                    p.Dispose();
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
