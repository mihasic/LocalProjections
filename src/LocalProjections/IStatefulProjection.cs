namespace LocalProjections
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IStatefulProjection : IDisposable
    {
        Task Project(Envelope message, CancellationToken cancellationToken);
        Task Commit(CancellationToken cancellationToken);
    }
}
