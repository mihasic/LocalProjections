namespace LocalProjections.Tests.Recipes.LmdbLucene
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using LightningStore;
    using LuceneSearch;

    using P = System.Collections.Generic.KeyValuePair<string, string>;

    public class Fixture : IDisposable
    {
        private readonly string _baseDirectory;
        private readonly Action<string> _writeLine;

        public readonly SimpleEventStore EventStore;
        public readonly PollingNotifier Notifier;
        public readonly RecoverableSubscriptionAdapter Subscription;
        public readonly ParallelGroupManager GroupManager;
        public readonly Index Index;

        public readonly ObjectRepository<string, SearchDocument> Repository;
        public readonly ObjectRepository<string, List<string>> ParentLookup;

        public Fixture(Action<string> writeLine)
        {
            _writeLine = writeLine ?? (_ => {});
            _baseDirectory = Path.Combine(Path.GetTempPath(), "local_projections", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_baseDirectory);
            EventStore = new SimpleEventStore(_baseDirectory);

            var searchDir = Path.Combine(_baseDirectory, "search");
            Directory.CreateDirectory(searchDir);
            Index = new Index(searchDir, new[]
            {
                new Field("name"),
                new Field("value", true),
                new Field("parent"),
                new Field("child"),
            });
            IEnumerable<P> ExtractDocument(string key, SearchDocument doc)
            {
                _writeLine($"Extracting {doc.Name}");
                yield return new P("name", doc.Name);
                yield return new P("value", doc.Value);
                foreach (var c in doc.Children ?? new string[0])
                    yield return new P("child", c);
                foreach (var p in doc.Parents ?? new string[0])
                    yield return new P("parent", p);
            }
            var searchProjector = new SearchProjector<string, SearchDocument>(
                Index,
                "name",
                ExtractDocument,
                d => d.Value != null);

            var mainProjectionDir = Path.Combine(_baseDirectory, "main");
            Directory.CreateDirectory(mainProjectionDir);
            Repository = new ObjectRepository<string, SearchDocument>(
                new DefaultObjectRepositorySettings<SearchDocument>(mainProjectionDir));

            var parentLookupDir = Path.Combine(_baseDirectory, "parent_lookup");
            Directory.CreateDirectory(parentLookupDir);
            ParentLookup = new ObjectRepository<string, List<string>>(
                new DefaultObjectRepositorySettings<List<string>>(parentLookupDir));

            Notifier = new PollingNotifier(EventStore.ReadHead, (ex, p) =>
            {
                _writeLine($"Error reading head ({p}): {ex}");
                return Task.CompletedTask;
            });

            GroupManager = new ParallelGroupManager(
                _baseDirectory,
                new Dictionary<string, Func<IStatefulProjection>>
                {
                    {
                        "main",
                        () => ProjectionHelpers.Combine(
                            ProjectionHelpers.Bind(
                                () => CachingRepositoryHelper.CreateSession(Repository, searchProjector),
                                repo => ProjectionHelpers.Bind(
                                    () => CachingRepositoryHelper.CreateSession(ParentLookup),
                                    parentLookup => (message, ct) =>
                                    {
                                        Handle(repo, parentLookup, message);
                                        return Task.CompletedTask;
                                    }
                                )
                            ),
                            (message) => _writeLine($"Projected {message.Checkpoint}: {message.Payload?.GetType().Name}"),
                            () =>
                            {
                                searchProjector.Commit();
                                _writeLine($"Committed");
                            }
                        )
                    }
                });

            Subscription = new RecoverableSubscriptionAdapter(
                CreateSubscription,
                () => Task.FromResult(GroupManager.CreateParallelGroup()),
                () => GroupManager.ProjectionGroupState.Min);
        }

        private void Handle(
            Lazy<CachingRepository<string, SearchDocument>> repo,
            Lazy<CachingRepository<string, List<string>>> parentLookup,
            Envelope message)
        {
            switch (message.Payload)
            {
                case DocumentSaved saved:
                    _writeLine($"Handling DocumentSaved {saved.Document.Name} ({message.Checkpoint})");
                    repo.Value.Run(saved.Document.Name, doc =>
                    {
                        var name = saved.Document.Name;

                        var removedChildren =
                            doc.Children?.Except(saved.Document.Children ?? Enumerable.Empty<string>()).Distinct() ??
                            Enumerable.Empty<string>();
                        var addedChildren =
                            saved.Document.Children?.Except(doc.Children ?? Enumerable.Empty<string>()).Distinct() ??
                            Enumerable.Empty<string>();

                        foreach (var c in removedChildren)
                        {
                            parentLookup.Value[c]?.Remove(name);
                            repo.Value[c].Parents = parentLookup.Value[c]?.ToArray();
                            if (parentLookup.Value[c]?.Any() != true)
                                parentLookup.Value.Delete(c);
                        }

                        foreach (var c in addedChildren)
                        {
                            parentLookup.Value.Run(c, l => l.Add(name));
                            repo.Value[c].Parents = parentLookup.Value[c].ToArray();
                        }

                        doc.Name = name;
                        doc.Value = saved.Document.Value;
                        doc.Children = saved.Document.Children;
                    });
                    _writeLine($"Handled DocumentSaved {saved.Document.Name} ({message.Checkpoint})");
                    break;
                default:
                    _writeLine($"Unknown event {message.Payload?.GetType().Name} ({message.Checkpoint})");
                    break;
            }
        }

        private Task<IDisposable> CreateSubscription(
            AllStreamPosition fromPosition,
            MessageReceived onMessage,
            HasCaughtUp hasCaughtUp,
            Func<Exception, Task> onSubscriptionError) =>
            Task.FromResult<IDisposable>(new GenericSubscription(
                (from, ct) => EventStore.ReadForward(from, cancellationToken: ct),
                fromPosition,
                Notifier.WaitForNotification,
                onMessage,
                onSubscriptionError,
                hasCaughtUp));

        public void Dispose()
        {
            Subscription.Dispose();
            GroupManager.Dispose();
            Notifier.Dispose();
            EventStore.Dispose();
            Repository.Dispose();
            ParentLookup.Dispose();
            Index.Dispose();

            try { Directory.Delete(_baseDirectory, true); } catch { }
        }
    }
}