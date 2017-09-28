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
        private readonly ProjectionGroupStateObserver _observer = new ProjectionGroupStateObserver();
        private readonly CreateSubscription _createSubscription;
        private readonly IReadOnlyDictionary<string, Func<IStatefulProjection>> _projectionGroups;
        private readonly ConcurrentDictionary<string, CheckpointStore> _checkpointStores =
            new ConcurrentDictionary<string, CheckpointStore>();
        private readonly object _sync = new object();
        private IDisposable _subscription = null;
        private string _baseDir;

        public RecoverableSubscriptionAdapter(
            CreateSubscription createSubscription,
            string baseDir,
            IReadOnlyDictionary<string, Func<IStatefulProjection>> projectionGroups)
        {
            _baseDir = Path.Combine(baseDir, "checkpoints");
            Directory.CreateDirectory(_baseDir);
            _createSubscription = createSubscription;
            _projectionGroups = projectionGroups;
        }

        private IStatefulProjection WrapProjection(
            string name,
            IStatefulProjection projection,
            int maxBatchSize = 2048)
        {
            var checkpointStore = _checkpointStores.GetOrAdd(name,
                _ => new CheckpointStore(Path.Combine(_baseDir, $"{name}.cpt")));
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

        public async Task Start()
        {
            _subscription?.Dispose();

            var projectionGroups = _projectionGroups
                .ToDictionary(x => x.Key, x => WrapProjection(x.Key, x.Value()));
            Func<IReadOnlyCollection<IStatefulProjection>> filtered =
                () => _observer.Active.Select(x => projectionGroups[x]).ToArray();
            var host = new ParallelExecutionHost(filtered);
            var subscr = await _createSubscription(
                _observer.Min,
                (s, m, ct) => host.Project(m, ct),
                () => host.Commit(),
                async ex =>
                {
                    // todo - use AutoResetEvent or something
                    await Start();
                }
                ).ConfigureAwait(false);
            _subscription = new DelegateDisposable(() =>
            {
                subscr.Dispose();
                host.Dispose();
                foreach (var p in _observer.Suspended.Select(x => projectionGroups[x]))
                    p.Dispose();
            });
            // TODO - dispose everything properly
            // TODO - lock
        }

        public async Task Restart()
        {
            _subscription?.Dispose();
            await Start().ConfigureAwait(false);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            foreach (var cpStore in _checkpointStores.Values)
                cpStore.Dispose();
            _checkpointStores.Clear();
        }
    }
}
