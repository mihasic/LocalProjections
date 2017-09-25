namespace LocalProjections
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using LightningStore;
    using LuceneSearch;

    public interface ISession : IDisposable
    {
        // void Project(object message);
        void Commit();
    }

    public class DelegateSession : ISession
    {
        private readonly Action _commit;
        private readonly Action _dispose;

        public DelegateSession(Action commit, Action dispose)
        {
            _commit = commit;
            _dispose = dispose;
        }

        public void Commit() => _commit();
        public void Dispose() => _dispose();
    }

    public class LocalProjectionGroup
    {
        private readonly int _maxBatchSize = 2048;
        private readonly Func<ISession> _getSession;
        private readonly CheckpointStore _checkpointStore;

        private ISession _session;

        private int _count = 0;

        public void Project(object message)
        {
            try
            {

            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {}
        }

        // TODO
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
