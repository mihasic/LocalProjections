namespace LocalProjections
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using LightningStore;

    public class LocalCheckpointGroup : IDisposable
    {
        private readonly ConcurrentDictionary<string, CheckpointStore> _checkpointStores =
            new ConcurrentDictionary<string, CheckpointStore>();
        private readonly string _checkpointsDir;

        public LocalCheckpointGroup(string checkpointsDir)
        {
            _checkpointsDir = checkpointsDir;
            Directory.CreateDirectory(_checkpointsDir);
        }

        public CheckpointStore GetCheckpointStore(string name) =>
            _checkpointStores.GetOrAdd(name,
                _ => new CheckpointStore(Path.Combine(_checkpointsDir, $"{name}.cpt")));
        public AllStreamPosition ReadCheckpoint(string name) =>
            _checkpointStores.TryGetValue(name, out CheckpointStore store)
                ? AllStreamPosition.FromNullableInt64(store.Read())
                : AllStreamPosition.None;

        public void Dispose()
        {
            foreach (var cpStore in _checkpointStores.Values)
                cpStore.Dispose();
            _checkpointStores.Clear();
        }
    }
}
