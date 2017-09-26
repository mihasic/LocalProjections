using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocalProjections.Tests
{
    internal class DelegateProjection : IStatefulProjection
    {
        private readonly Action _commit;
        private readonly Action _dispose;
        private readonly Action<Envelope> _project;

        public DelegateProjection(
            Action<Envelope> project = null,
            Action commit = null,
            Action dispose = null)
        {
            _project = project ?? (_ => {});
            _commit = commit ?? (() => {});
            _dispose = dispose ?? (() => {});
        }

        public Task Commit(CancellationToken cancellationToken)
        {
            _commit();
            return Task.CompletedTask;
        }

        public void Dispose() =>
            _dispose();

        public Task Project(Envelope message, CancellationToken cancellationToken)
        {
            _project(message);
            return Task.CompletedTask;
        }
    }
}
