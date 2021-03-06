namespace LocalProjections.RavenDb.XTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client;
    using Raven.Client.Document;
    using SqlStreamStore;
    using SqlStreamStore.Streams;
    using SqlStreamStore.Subscriptions;
    using P = System.Collections.Generic.KeyValuePair<string, string>;

    public class Fixture : IDisposable
    {
        private readonly Action<string> _writeLine;

        public readonly InMemoryStreamStore EventStore;
        public readonly RecoverableSubscriptionAdapter Subscription;
        public readonly Func<IAsyncDocumentSession> SessionFactory;
        public readonly ProjectionGroupStateObserver Observer = new ProjectionGroupStateObserver();

        public Fixture(Action<string> writeLine)
        {
            _writeLine = writeLine ?? (_ => {});
            EventStore = new InMemoryStreamStore();
            SessionFactory = RavenDb.CreateSessionFactory();

            Subscription = new RecoverableSubscriptionAdapter(
                CreateSubscription,
                () => CreateParallelGroup(),
                () => Observer.Min);

            using (var session = SessionFactory())
            {
                new SearchDocumentIndex().ExecuteAsync(session.GetAsyncDatabaseCommands(), new DocumentConvention())
                    .Wait();
            }
            _writeLine("Setup finished");
        }

        private async Task<IStatefulProjection> CreateParallelGroup()
        {
            var host = await ParallelGroupHelper.CreateObservableParallelGroup(
                async () => 
                {
                    await RavenCheckpointGroup.LoadCheckpoints(SessionFactory, Observer, new[] { "main" })
                        .ConfigureAwait(false);
                    _writeLine("Loaded checkpoints");

                    return new Dictionary<string, IStatefulProjection>
                    {
                        {
                            "main",
                            RavenCheckpointGroup.Wrap(Observer, "main", new DelegateProjection((e, ct) => Handle(e)))
                        }
                    };
                },
                Observer,
                () =>
                {
                    _writeLine("Restarting subscription");
                    Subscription.Restart();
                }
            ).ConfigureAwait(false);

            _writeLine("Created parallel host");
            return new StatefulProjectionBuilder(host)
                .Use(commitMidfunc: downstream => async ct =>
                {
                    _writeLine("Downstream commit");
                    await downstream(ct).ConfigureAwait(false);
                    _writeLine("Going to commit checkpoints");
                    await RavenCheckpointGroup.CommitActiveCheckpoints(SessionFactory, Observer, ct)
                        .ConfigureAwait(false);
                })
                .UseCommitEvery(maxBatchSize: 100)
                .Build();
        }

        private async Task Handle(Envelope envelope)
        {
            var message = (StreamMessage) envelope.Payload;
            switch (message.Type)
            {
                case nameof(DocumentSaved):
                    var saved = await message.GetJsonDataAs<DocumentSaved>();
                    _writeLine($"Handling DocumentSaved {saved.Document.Name} ({envelope.Checkpoint})");
                    using (var session = SessionFactory())
                    {
                        var name = saved.Document.Name;
                        var id = RavenDocument.GetId(name);

                        var doc = await session.LoadAsync<RavenDocument>(id).ConfigureAwait(false) ?? new RavenDocument();

                        var removedChildren =
                            doc.Children?.Except(saved.Document.Children ?? Enumerable.Empty<string>()).Distinct() ??
                            Enumerable.Empty<string>();
                        var addedChildren =
                            saved.Document.Children?.Except(doc.Children ?? Enumerable.Empty<string>()).Distinct() ??
                            Enumerable.Empty<string>();

                        foreach (var c in removedChildren)
                        {
                            var lookup = await session.LoadAsync<ParentLookup>(ParentLookup.GetId(c)).ConfigureAwait(false);
                            if (lookup == null) continue;
                            lookup.Parents?.Remove(name);
                            if (lookup.Parents?.Any() != true)
                                session.Delete(lookup);
                        }

                        foreach (var c in addedChildren)
                        {
                            var lookupId = ParentLookup.GetId(c);
                            var lookup = await session.LoadAsync<ParentLookup>(lookupId).ConfigureAwait(false);
                            if (lookup == null)
                            {
                                await session.StoreAsync(new ParentLookup
                                {
                                    Id = lookupId,
                                    DocumentId = RavenDocument.GetId(c),
                                    Parents = new List<string> { name }
                                }).ConfigureAwait(false);
                            }
                            else if (lookup.Parents?.Contains(name) != true)
                            {
                                lookup.Parents = lookup.Parents ?? new List<string>();
                                lookup.Parents.Add(name);
                            }
                        }

                        doc.Id = id;
                        doc.LookupId = ParentLookup.GetId(name);
                        doc.Name = name;
                        doc.Value = saved.Document.Value;
                        doc.Children = saved.Document.Children;

                        await session.StoreAsync(doc).ConfigureAwait(false);
                        await session.SaveChangesAsync().ConfigureAwait(false);
                    }
                    _writeLine($"Handled DocumentSaved {saved.Document.Name} ({envelope.Checkpoint})");
                    break;
                default:
                    _writeLine($"Unknown event {message.Type} ({envelope.Checkpoint})");
                    break;
            }
        }

        private Task<IDisposable> CreateSubscription(
            AllStreamPosition fromPosition,
            MessageReceived onMessage,
            LocalProjections.HasCaughtUp hasCaughtUp,
            Func<Exception, Task> onSubscriptionError)
        {
            SqlStreamStore.Subscriptions.AllStreamMessageReceived receivedHandler = (s, m) =>
                onMessage(s, new Envelope(new AllStreamPosition(m.Position), m), CancellationToken.None);
            SqlStreamStore.Subscriptions.AllSubscriptionDropped subscriptionDroppedHandler = (s, r, e) =>
                (r != SubscriptionDroppedReason.Disposed ? onSubscriptionError(e) : Task.CompletedTask)
                .GetAwaiter().GetResult();
            SqlStreamStore.Subscriptions.HasCaughtUp caughtUpHandler = caughtUp =>
                (caughtUp == true ? hasCaughtUp() : Task.CompletedTask).GetAwaiter().GetResult();

            var subscription = EventStore.SubscribeToAll(
                fromPosition.ToNullableInt64(),
                receivedHandler,
                subscriptionDroppedHandler,
                caughtUpHandler);

            return Task.FromResult<IDisposable>(subscription);
        }

        public void Dispose()
        {
            Subscription.Dispose();
            EventStore.Dispose();
        }
    }
}