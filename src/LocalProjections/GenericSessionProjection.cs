namespace LocalProjections
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class GenericSessionProjection<T> : IStatefulProjection
        where T : class, IDisposable
    {
        private readonly Func<Func<T>, Envelope, CancellationToken, Task> _project;
        private readonly Func<T> _createSession;
        private readonly Func<T, Task> _commitSession;

        private T _session;

        public GenericSessionProjection(
            Func<Func<T>, Envelope, CancellationToken, Task> project,
            Func<T> createSession,
            Func<T, Task> commitSession)
        {
            _project = project;
            _createSession = createSession;
            _commitSession = commitSession;
        }

        private T GetSession() =>
            _session ?? (_session = _createSession());

        public async Task Commit(CancellationToken cancellationToken = default(CancellationToken))
        {
            var s = _session;
            if (s != null)
            {
                await _commitSession(s).ConfigureAwait(false);
                s.Dispose();
                _session = null;
            }
        }

        public Task Project(Envelope message, CancellationToken cancellationToken = default(CancellationToken)) =>
            _project(GetSession, message, cancellationToken);

        public void Dispose()
        {
            _session?.Dispose();
            _session = null;
        }
    }
}
