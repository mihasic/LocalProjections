namespace LocalProjections
{
    using System;
    using System.Threading.Tasks;

    public delegate Task<IDisposable> CreateSubscription(
        AllStreamPosition fromPosition,
        MessageReceived onMessage,
        HasCaughtUp hasCaughtUp);
}