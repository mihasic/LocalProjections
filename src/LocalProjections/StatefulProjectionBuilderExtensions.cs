namespace LocalProjections
{
    using System;
    using LightningStore;

    public static class StatefulProjectionBuilderExtensions
    {
        public static StatefulProjectionBuilder UseCheckpointStore(
            this StatefulProjectionBuilder builder,
            CheckpointStore store,
            Action<AllStreamPosition> notifyCheckpoint = null) =>
            builder.Use(p => new CheckpointProjection(p, store, notifyCheckpoint));

        public static StatefulProjectionBuilder UseCommitEvery(
            this StatefulProjectionBuilder builder,
            int maxBatchSize = 2048) =>
            builder.Use(p => new CommitNthProjection(p, maxBatchSize));

        public static StatefulProjectionBuilder UseSuspendOnException(
            this StatefulProjectionBuilder builder,
            Action<Exception> onSuspend = null) =>
            builder.Use(p => new SuspendableProjection(p, onSuspend));
    }
}
