namespace LocalProjections
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using LightningStore;
    using LuceneSearch;

    public class LocalProjectionGroup
    {
        public static IStatefulProjection WrapProjection<T>(
            string name,
            CheckpointStore checkpointStore,
            GenericSessionProjection<T> projection,
            int maxBatchSize = 2048)
            where T : class, IDisposable
        {
            var state = new ProjectionGroupState(name);

            return new SuspendableProjection(
                new CommitNthProjection(
                    new CheckpointProjection(
                        projection,
                        checkpointStore,
                        cp => state = state.MoveTo(cp)
                    ),
                    maxBatchSize
                ),
                ex => state = state.Suspend(ex)
            );
        }
    }
}
