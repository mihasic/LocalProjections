namespace LocalProjections
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using LightningStore;

    // TODO - extract checkpoint store

    public class ParallelGroupManager : IDisposable
    {
        private readonly IReadOnlyDictionary<string, Func<IStatefulProjection>> _projectionGroups;
        private readonly string _checkpointsDir;
        private readonly ProjectionGroupStateObserver _observer = new ProjectionGroupStateObserver();
        private readonly ConcurrentDictionary<string, CheckpointStore> _checkpointStores =
            new ConcurrentDictionary<string, CheckpointStore>();

        public ParallelGroupManager(
            string baseDir,
            IReadOnlyDictionary<string, Func<IStatefulProjection>> projectionGroups)
        {
            _checkpointsDir = Path.Combine(baseDir, "checkpoints");
            Directory.CreateDirectory(_checkpointsDir);
            _projectionGroups = projectionGroups.ToDictionary(
                x => x.Key,
                x => new StatefulProjectionBuilder(x.Value)
                    .UseCheckpointStore(
                        GetCheckpointStore(x.Key),
                        cp => _observer[x.Key] = _observer[x.Key].MoveTo(cp))
                    .UseCommitEvery(maxBatchSize: 2048)
                    .UseSuspendOnException(ex => _observer[x.Key] = _observer[x.Key].Suspend(ex))
                    .BuildFactory());
        }

        public IProjectionGroupStateObserver ProjectionGroupState => _observer;

        public AllStreamPosition ReadCheckpoint(string name) =>
            _checkpointStores.TryGetValue(name, out CheckpointStore store)
                ? AllStreamPosition.FromNullableInt64(store.Read())
                : AllStreamPosition.None;

        private CheckpointStore GetCheckpointStore(string name) =>
            _checkpointStores.GetOrAdd(name,
                _ => new CheckpointStore(Path.Combine(_checkpointsDir, $"{name}.cpt")));

        public IStatefulProjection CreateParallelGroup()
        {
            var projectionGroups = _projectionGroups
                .ToDictionary(x => x.Key, x => x.Value());
            Func<IReadOnlyCollection<IStatefulProjection>> filtered =
                () => _observer.Active.Select(x => projectionGroups[x]).ToArray();
            var host = new ParallelExecutionHost(filtered);

            return new StatefulProjectionBuilder(host)
                .Use(disposeMidfunc: downstream => () =>
                {
                    downstream();
                    foreach (var p in projectionGroups.Values)
                        p.Dispose();
                })
                .Build();
        }

        public AllStreamPosition GetStartingPosition() =>
            _observer.Min;

        public void Dispose()
        {
            foreach (var cpStore in _checkpointStores.Values)
                cpStore.Dispose();
            _checkpointStores.Clear();
        }

    }
}
