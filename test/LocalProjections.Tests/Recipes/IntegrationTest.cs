namespace LocalProjections.Tests.Recipes
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Shouldly;
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
            var maxCheckpoint = await _fixture.EventStore.ReadHead().ConfigureAwait(false);
            while (sw.ElapsedMilliseconds < timeout)
            {
                if (_fixture.Observer.All.Any())
                {
                    //if (_fixture.Subscription.ProjectionGroupState.Max >= maxCheckpoint)
                    var name = _fixture.Observer.All.First().Key;
                    var cp = _fixture.CheckpointsGroup.ReadCheckpoint(name);
                    if (cp >= maxCheckpoint)
                        return;
                }
                await Task.Delay(300);
            }
            throw new TimeoutException();
        }

        private Task SaveDoc(string name, string value, string[] children = null) =>
            _fixture.EventStore.Append(new DocumentSaved
            {
                Document = new Document
                {
                    Name = name,
                    Value = value,
                    Children = children
                }});

        [Fact]
        public async Task Can_append_messages_and_search()
        {
            _fixture.Subscription.Start();

            await SaveDoc("test1", "some value1");
            await SaveDoc("test2", "some value2");
            await Wait();
            var (total, _, docs) = _fixture.Index.Search(
                "value:some AND value:*1 AND name:*1",
                fieldsToLoad: new HashSet<string>() { "name" });
            total.ShouldBe(1);
            docs.Single().Single().Value.ShouldBe("test1");
            var projected = _fixture.Repository.Get("test1");
            projected.Name.ShouldBe("test1");
            projected.Value.ShouldBe("some value1");
        }

        [Fact]
        public async Task Handles_parents_properly()
        {
            _fixture.Subscription.Start();

            await SaveDoc("test1", "some value1", new[] { "test2" });
            await SaveDoc("test2", "some value2");
            await Wait();
            var (total, _, docs) = _fixture.Index.Search(
                "parent:test1",
                fieldsToLoad: new HashSet<string>() { "name" });
            total.ShouldBe(1);
            docs.Single().Single().Value.ShouldBe("test2");
            var projected = _fixture.Repository.Get("test2");
            projected.Name.ShouldBe("test2");
            projected.Value.ShouldBe("some value2");
            projected.Parents.ShouldBe(new[] { "test1" });
            projected.Children.ShouldBeNull();
        }

        public void Dispose() => _fixture.Dispose();
    }
}