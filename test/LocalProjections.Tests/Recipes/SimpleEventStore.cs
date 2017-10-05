namespace LocalProjections.Tests.Recipes
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using LightningStore;

    public class SimpleEventStore : IDisposable
    {
        private readonly ChangeStream _allStream;
        private readonly PollingNotifier _notifier;

        public SimpleEventStore(string baseDirectory)
        {
            var directoryName = Path.Combine(baseDirectory, "EventStore");
            Directory.CreateDirectory(directoryName);
            _allStream = new ChangeStream(directoryName);
            _notifier = new PollingNotifier(ReadHead);
        }

        public Task<AllStreamPosition> Append(
            DocumentSaved @event,
            CancellationToken cancellationToken = default(CancellationToken)) =>
            Task.FromResult(TranslateKey(_allStream.Append(Serializer.SerializeJson(@event))));

        public Task<AllStreamPosition> ReadHead(CancellationToken cancellationToken = default(CancellationToken)) =>
            Task.FromResult(TranslateKey(_allStream.GetLastCheckpoint().Key));

        public Task<ReadAllPage> ReadForward(
            AllStreamPosition from = default(AllStreamPosition),
            int batchSize = 10,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var pairs = _allStream.ReadAfter(from.ToInt64()-1, batchSize);

            var messages = pairs.Select(p => new Envelope(
                TranslateKey(p.Key),
                Serializer.DeserializeJson<DocumentSaved>(p.Value)))
                .ToArray();

            if (messages.Length == 0)
                return Task.FromResult(new ReadAllPage(from, from, true, new Envelope[0]));

            var next = messages.Last().Checkpoint.Shift();
            var isEnd = _allStream.GetLastCheckpoint().Key < next;
            return Task.FromResult(new ReadAllPage(from, next, isEnd, messages));
        }

        private AllStreamPosition TranslateKey(long key) =>
            key < 0 ? AllStreamPosition.None : new AllStreamPosition(key);

        /// <summary>
        /// Subscribes to latest changes.
        /// Only supports a single subscriber at a time, based on notifier implementation.
        /// </summary>
        public Task<IDisposable> Subscribe(
            AllStreamPosition fromPosition,
            MessageReceived onMessage,
            HasCaughtUp hasCaughtUp,
            Func<Exception, Task> onSubscriptionError) =>
            Task.FromResult<IDisposable>(
                new GenericSubscription(
                    (f, ct) => ReadForward(f, 10, ct),
                    fromPosition,
                    _notifier.WaitForNotification,
                    onMessage,
                    onSubscriptionError,
                    hasCaughtUp));

        public void Dispose()
        {
            _notifier.Dispose();
            _allStream.Dispose();
        }
    }
}