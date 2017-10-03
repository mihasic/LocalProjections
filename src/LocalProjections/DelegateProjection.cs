namespace LocalProjections
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class DelegateProjection : IStatefulProjection
    {
        internal readonly Func<CancellationToken, Task> CommitFunc;
        internal readonly Action DisposeFunc;
        internal readonly Func<Envelope, CancellationToken, Task> ProjectFunc;

        public DelegateProjection(
            Func<Envelope, CancellationToken, Task> project = null,
            Func<CancellationToken, Task> commit = null,
            Action dispose = null)
            : this()
        {
            ProjectFunc = project ?? ProjectFunc;
            CommitFunc = commit ?? CommitFunc;
            DisposeFunc = dispose ?? DisposeFunc;
        }

        private DelegateProjection()
        {
            ProjectFunc = (_, __) => Task.CompletedTask;
            CommitFunc = _ => Task.CompletedTask;
            DisposeFunc = () => {};
        }

        public Task Commit(CancellationToken cancellationToken) =>
                CommitFunc(cancellationToken);

        public void Dispose() =>
            DisposeFunc();

        public Task Project(Envelope message, CancellationToken cancellationToken) =>
                ProjectFunc(message, cancellationToken);
    }
}
