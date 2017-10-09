namespace LocalProjections.RavenDb.XTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client;

    public static class RavenCheckpointGroup
    {
        public static async Task LoadCheckpoints(
            Func<IAsyncDocumentSession> sessionFactory,
            ProjectionGroupStateObserver observer,
            IEnumerable<string> keys)
        {
            using (var session = sessionFactory())
            {
                var docs = await session.LoadAsync<ProjectionPosition>(
                    keys.Select(ProjectionPosition.GetIdFromName)).ConfigureAwait(false);
                var pairs = keys.Zip(docs, (k, d) =>
                    new KeyValuePair<string, AllStreamPosition>(k, AllStreamPosition.FromNullableInt64(d?.Position)));
                foreach (var pair in pairs)
                {
                    observer.MoveTo(pair.Key, pair.Value);
                }
            }
        }

        public static IStatefulProjection Wrap(
            ProjectionGroupStateObserver observer,
            string name,
            IStatefulProjection projection) =>
            new StatefulProjectionBuilder(projection)
                .Use(projectMidfunc: downstream => async (message, ct) =>
                {
                    if (observer[name].Checkpoint >= message.Checkpoint)
                        return;
                    await downstream(message, ct).ConfigureAwait(false);
                    observer.MoveTo(name, message.Checkpoint);
                })
                .UseSuspendOnException(ex => observer.Suspend(name, ex))
                .Build();

        public static async Task CommitActiveCheckpoints(
            Func<IAsyncDocumentSession> sessionFactory,
            ProjectionGroupStateObserver observer,
            CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return;
            using (var session = sessionFactory())
            {
                var activePositionDocuments = observer.Active.Select(x => new ProjectionPosition
                {
                    Id = ProjectionPosition.GetIdFromName(x),
                    Position = observer[x].Checkpoint.ToNullableInt64()
                });
                foreach (var p in activePositionDocuments)
                    await session.StoreAsync(p, ct).ConfigureAwait(false);

                await session.SaveChangesAsync(ct).ConfigureAwait(false);
            }
        }
    }
}