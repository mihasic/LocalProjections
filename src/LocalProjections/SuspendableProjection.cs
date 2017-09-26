namespace LocalProjections
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class SuspendableProjection : IStatefulProjection
    {
        private readonly IStatefulProjection _inner;
        private readonly Action<Exception> _onSuspend;
        private bool _suspended = false;

        public SuspendableProjection(IStatefulProjection inner, Action<Exception> onSuspend = null)
        {
            _inner = inner;
            _onSuspend = onSuspend;
        }

        private void Suspend(Exception ex)
        {
            _suspended = true;
            _onSuspend?.Invoke(ex);
        }

        public async Task Project(Envelope message, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (_suspended) return;
            await Extensions.HandleException(() => _inner.Project(message, cancellationToken), Suspend);
        }

        public async Task Commit(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (_suspended) return;
            await Extensions.HandleException(() => _inner.Commit(cancellationToken), Suspend);
        }

        public void Dispose() => _inner.Dispose();
    }
}
