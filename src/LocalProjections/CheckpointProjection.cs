namespace LocalProjections
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using LightningStore;

    public class CheckpointProjection : IStatefulProjection
    {
        private readonly IStatefulProjection _inner;
        private readonly CheckpointStore _checkpointStore;
        private readonly Action<AllStreamPosition> _notifyCheckpoint;
        private long? _lastCheckpoint;

        public CheckpointProjection(
            IStatefulProjection inner,
            CheckpointStore checkpointStore,
            Action<AllStreamPosition> notifyCheckpoint = null)
        {
            _inner = inner;
            _checkpointStore = checkpointStore;
            _notifyCheckpoint = notifyCheckpoint;

            _lastCheckpoint = _checkpointStore.Read();
            _notifyCheckpoint?.Invoke(AllStreamPosition.FromNullableInt64(_lastCheckpoint));
        }

        public async Task Commit(CancellationToken cancellationToken = default(CancellationToken))
        {
            await _inner.Commit(cancellationToken).ConfigureAwait(false);
            _checkpointStore.Write(_lastCheckpoint);
        }

        public async Task Project(Envelope message, CancellationToken cancellationToken = default(CancellationToken))
        {
            var checkpoint = message.Checkpoint;
            if (_lastCheckpoint == null || checkpoint > _lastCheckpoint)
            {
                await _inner.Project(message, cancellationToken).ConfigureAwait(false);
                _lastCheckpoint = checkpoint;
                _notifyCheckpoint?.Invoke(AllStreamPosition.FromNullableInt64(_lastCheckpoint));
            }
        }

        public void Dispose() => _inner.Dispose();
    }
}
