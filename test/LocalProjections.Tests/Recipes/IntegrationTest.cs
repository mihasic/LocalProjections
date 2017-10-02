namespace LocalProjections.Tests.Recipes
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
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
            while (_fixture.Subscription.ProjectionGroupState.Max < maxCheckpoint &&
                   sw.ElapsedMilliseconds < timeout)
            {
                await Task.Delay(300);
            }
            if (_fixture.Subscription.ProjectionGroupState.Max < maxCheckpoint)
                throw new TimeoutException();
        }

        private Task SaveDoc(string name, string value, string[] children = null) =>
            _fixture.EventStore.Append(new DocumentSaved
            {
                Document = new Document
                {
                    Name = "test",
                    Value = "test val1",
                    Children = children
                }});

        [Fact]
        public async Task Can_append_messages_and_search()
        {
            _fixture.Subscription.Start();

            await SaveDoc("test1", "some value1");
            await SaveDoc("test2", "some value2");
            await Wait();
            //_fixture.Index.Search()
        }

        public void Dispose() => _fixture.Dispose();
    }
}