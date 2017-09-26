namespace LocalProjections.Tests
{
    using System;
    using System.IO;
    using LightningStore;

    internal static class Util
    {
        public static CheckpointStore BuildCheckpointStore() =>
            new CheckpointStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
    }
}