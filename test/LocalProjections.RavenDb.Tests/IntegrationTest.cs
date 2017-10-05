namespace LocalProjections.RavenDb.Tests
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
        public IntegrationTest(ITestOutputHelper output)
        {
            _fixture = new Fixture(output.WriteLine);
        }

        private async Task Wait(int timeout = 3000)
        {
            var sw = Stopwatch.StartNew();
            var maxCheckpoint = await _fixture.EventStore.ReadHeadPosition(CancellationToken.None).ConfigureAwait(false);
            AllStreamPosition checkpoint = AllStreamPosition.None;
            while (sw.ElapsedMilliseconds < timeout)
            {
                //if (_fixture.Subscription.ProjectionGroupState.Max >= maxCheckpoint)
                var name = _fixture.GroupManager.ProjectionGroupState.All.First().Key;
                var id = ProjectionPosition.GetIdFromName(name);
                using (var s = _fixture.SessionFactory())
                {
                    var cp = await s.LoadAsync<ProjectionPosition>(id).ConfigureAwait(false);
                    checkpoint = AllStreamPosition.FromNullableInt64(cp?.Position);
                }
                if (checkpoint >= maxCheckpoint)
                    return;
                await Task.Delay(300);
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

        [Fact(Skip="TODO")]
        public async Task Can_append_messages_and_search()
        {
            _fixture.Subscription.Start();

            await SaveDoc("test1", "some value1");
            await SaveDoc("test2", "some value2");
            await Wait();
            // var (total, _, docs) = _fixture.Index.Search(
            //     "value:some AND value:*1 AND name:*1",
            //     fieldsToLoad: new HashSet<string>() { "name" });
            // total.ShouldBe(1);
            // docs.Single().Single().Value.ShouldBe("test1");
            // var projected = _fixture.Repository.Get("test1");
            // projected.Name.ShouldBe("test1");
            // projected.Value.ShouldBe("some value1");
        }

        [Fact(Skip="TODO")]
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
                    .Where(x => "test1".In(x.Parents))
                    .Take(10)
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