namespace LocalProjections
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class ParallelExecutionHost : IStatefulProjection
    {
        private readonly int _maxDegreeOfParallelism;
        private readonly Func<IReadOnlyCollection<IStatefulProjection>> _groups;

        public ParallelExecutionHost(
            Func<IReadOnlyCollection<IStatefulProjection>> groups,
            int maxDegreeOfParallelism = 2)
        {
            _groups = groups;
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        public Task Commit(CancellationToken cancellationToken = default(CancellationToken)) =>
            RunPartitions(g => g.Commit(cancellationToken), cancellationToken);

        public void Dispose()
        {
        }

        public Task Project(Envelope message, CancellationToken cancellationToken = default(CancellationToken)) =>
            RunPartitions(g => g.Project(message, cancellationToken), cancellationToken);

        private Task RunPartitions(Func<IStatefulProjection, Task> action, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.CompletedTask;

            var partitions = Partitioner.Create(_groups())
                .GetPartitions(_maxDegreeOfParallelism)
                .ToArray();

            if (partitions.Length == 0) return Task.CompletedTask;

            return Task.WhenAll(partitions.Select(gs => RunPartition(gs, action, cancellationToken)));
        }
        private async Task RunPartition(
            IEnumerator<IStatefulProjection> partition,
            Func<IStatefulProjection, Task> action,
            CancellationToken cancellationToken)
        {
            using (partition)
            {
                while (partition.MoveNext() && !cancellationToken.IsCancellationRequested)
                {
                    await action(partition.Current);
                }
            }
        }
    }
}