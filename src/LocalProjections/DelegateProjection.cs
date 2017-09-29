namespace LocalProjections
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class DelegateProjection : IStatefulProjection
    {
        private readonly Func<CancellationToken, Task> _commit;
        private readonly Action _dispose;
        private readonly Func<Envelope, CancellationToken, Task> _project;

        public DelegateProjection(
            Func<Envelope, CancellationToken, Task> project,
            Func<CancellationToken, Task> commit,
            Action dispose)
        {
            _project = project ?? _project;
            _commit = commit ?? _commit;
            _dispose = dispose ?? _dispose;
        }

        public DelegateProjection(
            Action<Envelope> project = null,
            Action commit = null)
            : this()
        {
            if (project != null)
                _project = (m, ct) => { project(m); return Task.CompletedTask; };
            if (commit != null)
                _commit = _ => { commit(); return Task.CompletedTask; };
        }

        private DelegateProjection()
        {
            _project = (_, __) => Task.CompletedTask;
            _commit = _ => Task.CompletedTask;
            _dispose = () => {};
        }

        public Task Commit(CancellationToken cancellationToken) =>
            cancellationToken.IsCancellationRequested
                ? Task.CompletedTask
                : _commit(cancellationToken);

        public void Dispose() =>
            _dispose();

        public Task Project(Envelope message, CancellationToken cancellationToken) =>
            cancellationToken.IsCancellationRequested
                ? Task.CompletedTask
                : _project(message, cancellationToken);
    }
}
