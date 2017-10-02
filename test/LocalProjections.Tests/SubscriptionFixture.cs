namespace LocalProjections.Tests
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class SubscriptionFixture : IDisposable
    {
        private readonly CancellationTokenSource _cts;
        private readonly TaskCompletionSource<Exception> _exceptionSource = new TaskCompletionSource<Exception>();
        private readonly Action<string> _writeLine;

        public readonly GenericSubscription Subscription;
        public readonly Func<Task> WaitForCaughtUp;
        public AllStreamPosition LastProcessed { get; private set; }
        public long MaxPosition { get; private set; } = 20;
        public Task<Exception> WaitForException => _exceptionSource.Task;
        public readonly Action<uint> AppendEvents;

        public SubscriptionFixture(Action<string> writeLine,
            long maxPosition = 20,
            long handlerExceptionPosition = -2)
        {
            _writeLine = writeLine;
            _cts = new CancellationTokenSource();
            MaxPosition = maxPosition;
            AllStreamPosition hasCaughtUpTrigger = AllStreamPosition.None;
            AllStreamPosition notifierTrigger = AllStreamPosition.None;
            ReadAllPageFunc readAllPage = (from, ct) => Task.FromResult(Read(from));
            MessageReceived handler = (s, m, ct) =>
            {
                LastProcessed = m.Checkpoint;
                if (m.Checkpoint == handlerExceptionPosition)
                    throw new InvalidOperationException("Custom exception thrown");
                return Task.CompletedTask;
            };
            Func<Exception, Task> onError = ex => Async(() => _exceptionSource.SetResult(ex));
            HasCaughtUp hasCaughtUp = () => Async(() => hasCaughtUpTrigger = hasCaughtUpTrigger.Shift());

            var hasCaughtUpNotifier = new PollingNotifier(_ => Task.FromResult(hasCaughtUpTrigger));
            var notifier = new PollingNotifier(_ => Task.FromResult(notifierTrigger));

            Subscription = new GenericSubscription(
                readAllPage,
                AllStreamPosition.None,
                notifier.WaitForNotification,
                handler,
                onError,
                hasCaughtUp);

            var cancellationToken = _cts.Token;

            cancellationToken.Register(() =>
            {
                Subscription.Dispose();
                notifier.Dispose();
                hasCaughtUpNotifier.Dispose();
            });

            WaitForCaughtUp = () => hasCaughtUpNotifier.WaitForNotification(cancellationToken);
            AppendEvents = c =>
            {
                MaxPosition += c;
                notifierTrigger = notifierTrigger.Shift();
            };
        }

        public void Dispose() => _cts.Cancel();

        private ReadAllPage Read(AllStreamPosition from)
        {
            var maxPosition = MaxPosition;
            _writeLine($"From {from}, Max {maxPosition}");
            if (from > MaxPosition)
            {
                var page = new ReadAllPage(from, from, true, new Envelope[0]);
                _writeLine(page.ToString());
                return page;
            }
            var messages = Enumerable.Range(0, 10)
                .Where(x => x <= maxPosition - from.ToInt64())
                .Select(x => new Envelope(from.Shift(x), x))
                .ToArray();
            bool isEnd = !messages.Any() || messages.Last().Checkpoint.ToInt64() == maxPosition;
            var result = new ReadAllPage(
                from,
                messages.Any() ? messages.Last().Checkpoint.Shift() : from,
                isEnd,
                messages);
            _writeLine(result.ToString());
            return result;
        }

        private static Task Async(Action a)
        {
            a();
            return Task.CompletedTask;
        }
    }
}