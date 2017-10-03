namespace LocalProjections.Tests
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using LightningStore;

    internal static class Util
    {
        public static CheckpointStore BuildCheckpointStore() =>
            new CheckpointStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        public static DelegateProjection Projection(
            Action<Envelope> project = null,
            Action commit = null,
            Action dispose = null) =>
            new DelegateProjection(
                project == null
                    ? (Func<Envelope, CancellationToken, Task>)null
                    : ((m, ct) => { project(m); return Task.CompletedTask; }),
                commit == null
                    ? (Func<CancellationToken, Task>)null
                    : (ct => { commit(); return Task.CompletedTask; }),
                dispose);


    }
}