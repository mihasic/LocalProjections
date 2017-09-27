namespace LocalProjections.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Xunit;

    public class GenericSubscriptionTests
    {
        [Fact]
        public async Task Test()
        {
            AllStreamPosition position = AllStreamPosition.None;
            ReadAllPageFunc readAllPage = (from, ct) => Task.FromResult(new ReadAllPage(
                from,
                from.Shift(10),
                (from.ToInt64()+10) % 20 == 0,
                Enumerable.Range(0, 10).Select(x => new Envelope(from.Shift(x), x)).ToArray()
            ));
            MessageReceived handler = null;
            Func<Exception, Task> onError = null;
            HasCaughtUp hasCaughtUp = null;

            using (var notifier = new PollingNotifier(_ => Task.FromResult(position)))
            using (var s = new GenericSubscription(
                readAllPage,
                AllStreamPosition.None,
                notifier.WaitForNotification,
                handler,
                onError,
                hasCaughtUp))
            {
                await s.Started.ConfigureAwait(false);
            }
        }
    }
}