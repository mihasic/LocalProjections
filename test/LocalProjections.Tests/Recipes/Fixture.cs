namespace LocalProjections.Tests.Recipes
{
    using System;
    using System.IO;
    using LightningStore;

    public class Fixture : IDisposable
    {
        private readonly string _baseDirectory;

        public readonly SimpleEventStore EventStore;

        public Fixture()
        {
            _baseDirectory = Path.Combine(Path.GetTempPath(), "LocalProjections", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_baseDirectory);
            EventStore = new SimpleEventStore(_baseDirectory);
        }

        public void Dispose()
        {
            EventStore.Dispose();

            try { Directory.Delete(_baseDirectory, true); } catch { }
        }
    }
}