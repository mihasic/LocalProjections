namespace LocalProjections
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public delegate Task MessageReceived(
        IDisposable subscription,
        Envelope message,
        CancellationToken cancellationToken);
}