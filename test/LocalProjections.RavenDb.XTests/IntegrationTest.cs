namespace LocalProjections.RavenDb.XTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client;
    using Raven.Client.Linq;
    using Shouldly;
    using SqlStreamStore.Streams;
    using StreamStoreStore.Json;
    using Xunit;
    using Xunit.Abstractions;

    public class IntegrationTest : IDisposable
    {
        private readonly Fixture _fixture;
        private readonly Action<string> _writeLine;

        public IntegrationTest(ITestOutputHelper output)
        {
            _writeLine = output.WriteLine;
            _fixture = new Fixture(output.WriteLine);
        }

        private async Task Wait(int timeout = 3000)
        {
            var sw = Stopwatch.StartNew();
            var maxCheckpoint = await _fixture.EventStore.ReadHeadPosition(CancellationToken.None).ConfigureAwait(false);
            AllStreamPosition checkpoint = AllStreamPosition.None;
            while (sw.ElapsedMilliseconds < timeout)
            {
                if (_fixture.Observer.All.Any())
                {
                    // var name = _fixture.Observer.All.First().Key;
                    // var id = ProjectionPosition.GetIdFromName(name);
                    // using (var s = _fixture.SessionFactory())
                    // {
                    //     var cp = await s.LoadAsync<ProjectionPosition>(id).ConfigureAwait(false);
                    //     checkpoint = AllStreamPosition.FromNullableInt64(cp?.Position);
                    // }
                    if (_fixture.Observer.Min >= maxCheckpoint)
                        return;
                }
                await Task.Delay(300);
            }
            foreach (var state in _fixture.Observer.All.Values)
            {
                _writeLine($"[{state.Name}]: {state.Checkpoint} {state.Suspended} {state.Exception}");
            }
            throw new TimeoutException();
        }

        private Task SaveDoc(string name, string value, string[] children = null) =>
            _fixture.EventStore.AppendToStream(name, ExpectedVersion.Any, new[] {
                new NewStreamMessage(Guid.NewGuid(), nameof(DocumentSaved), SimpleJson.SerializeObject(new DocumentSaved
                {
                    Document = new Document
                    {
                        Name = name,
                        Value = value,
                        Children = children
                    }
                }))
            });

        [Fact]
        public async Task Can_append_messages_and_search()
        {
            _fixture.Subscription.Start();

            await SaveDoc("test1", "some value1");
            await SaveDoc("test2", "some value2");
            await Wait();

            RavenQueryStatistics stat;
            IEnumerable<SearchDocument> docs;
            using (var session = _fixture.SessionFactory())
            {
                docs = await session.Query<SearchDocument, SearchDocumentIndex>()
                    .Customize(x => x.WaitForNonStaleResultsAsOfLastWrite())
                    .Statistics(out stat)
                    .Search(x => x.Value, "some", options: SearchOptions.And)
                    .Search(x => x.Value, "*1", options: SearchOptions.And, escapeQueryOptions: EscapeQueryOptions.AllowAllWildcards)
                    .Take(10)
                    .ProjectFromIndexFieldsInto<SearchDocument>()
                    .ToListAsync()
                    .ConfigureAwait(false);
            }

            stat.TotalResults.ShouldBe(1);
            var doc = docs.Single();
            doc.Name.ShouldBe("test1");
            doc.Value.ShouldBe("some value1");
            doc.Parents.ShouldBeNull();
            doc.Children.ShouldBeNull();
        }

        [Fact]
        public async Task Handles_parents_properly()
        {
            _fixture.Subscription.Start();

            await SaveDoc("test1", "some value1", new[] { "test2" });
            await SaveDoc("test2", "some value2");
            await Wait();

            RavenQueryStatistics stat;
            IEnumerable<SearchDocument> docs;
            using (var session = _fixture.SessionFactory())
            {
                docs = await session.Query<SearchDocument, SearchDocumentIndex>()
                    .Customize(x => x.WaitForNonStaleResultsAsOfLastWrite())
                    .Statistics(out stat)
                    .Where(x => x.Parents.Contains("test1"))
                    .Take(10)
                    .ProjectFromIndexFieldsInto<SearchDocument>()
                    .ToListAsync()
                    .ConfigureAwait(false);
            }

            stat.TotalResults.ShouldBe(1);
            var doc = docs.Single();
            doc.Name.ShouldBe("test2");
            doc.Name.ShouldBe("test2");
            doc.Value.ShouldBe("some value2");
            doc.Parents.ShouldBe(new[] { "test1" });
            doc.Children.ShouldBeNull();
        }

        public void Dispose() => _fixture.Dispose();
    }
}