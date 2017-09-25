using System.Threading;
using System.Threading.Tasks;

namespace LocalProjections
{
    public class CommitNthProjection : IStatefulProjection
    {
        private readonly IStatefulProjection _inner;
        private readonly int _maxBatchSize;

        private int _currentSize = 0;

        public CommitNthProjection(IStatefulProjection inner, int maxBatchSize = 2048)
        {
            _inner = inner;
            _maxBatchSize = maxBatchSize;
        }

        public async Task Commit(CancellationToken cancellationToken = default(CancellationToken))
        {
            await _inner.Commit(cancellationToken).ConfigureAwait(false);
            _currentSize = 0;
        }

        public async Task Project(Envelope message, CancellationToken cancellationToken = default(CancellationToken))
        {
            await _inner.Project(message, cancellationToken).ConfigureAwait(false);
            _currentSize++;

            if (_currentSize >= _maxBatchSize)
            {
                await Commit(cancellationToken).ConfigureAwait(false);
            }
        }

        public void Dispose() => _inner.Dispose();
    }
}
