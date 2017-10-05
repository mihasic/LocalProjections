namespace LocalProjections.RavenDb.XTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Raven.Client;
    using Raven.Client.Document;

    public class RavenGroupManager : IDisposable
    {
        private readonly IReadOnlyDictionary<string, Func<IStatefulProjection>> _projectionGroups;
        private readonly ProjectionGroupStateObserver _observer = new ProjectionGroupStateObserver();
        private readonly Func<IAsyncDocumentSession> _sessionFactory;

        public RavenGroupManager(
            Func<IAsyncDocumentSession> sessionFactory,
            IReadOnlyDictionary<string, Func<IStatefulProjection>> projectionGroups)
        {
            _sessionFactory = sessionFactory;
            _projectionGroups = projectionGroups.ToDictionary(
                x => x.Key,
                x => new StatefulProjectionBuilder(x.Value)
                    .Use(projectMidfunc: downstream => async (message, ct) =>
                    {
                        if (_observer[x.Key].Checkpoint >= message.Checkpoint)
                            return;
                        await downstream(message, ct).ConfigureAwait(false);
                        _observer[x.Key] = _observer[x.Key].MoveTo(message.Checkpoint);
                    })
                    .UseSuspendOnException(ex => _observer[x.Key] = _observer[x.Key].Suspend(ex))
                    .BuildFactory());
        }

        public IProjectionGroupStateObserver ProjectionGroupState => _observer;

        public async Task<IStatefulProjection> CreateParallelGroup()
        {
            using (var session = _sessionFactory())
            {
                var docs = await session.LoadAsync<ProjectionPosition>(
                    _projectionGroups.Keys.Select(ProjectionPosition.GetIdFromName));
                var pairs = _projectionGroups.Keys.Zip(docs, (k, d) =>
                    new KeyValuePair<string, AllStreamPosition>(k, AllStreamPosition.FromNullableInt64(d?.Position)));
                foreach (var pair in pairs)
                {
                    _observer[pair.Key] = _observer[pair.Key].MoveTo(pair.Value);
                }
            }

            var projectionGroups = _projectionGroups.ToDictionary(x => x.Key, x => x.Value());

            Func<IReadOnlyCollection<IStatefulProjection>> filtered =
                () => _observer.Active.Select(x => projectionGroups[x]).ToArray();

            var host = new ParallelExecutionHost(filtered);

            return new StatefulProjectionBuilder(host)
                .Use(commitMidfunc: downstream => async ct =>
                {
                    await downstream(ct);
                    using (var session = _sessionFactory())
                    {
                        var activePositionDocuments = _observer.Active.Select(x => new ProjectionPosition
                        {
                            Id = ProjectionPosition.GetIdFromName(x),
                            Position = _observer[x].Checkpoint.ToNullableInt64()
                        });
                        foreach (var p in activePositionDocuments)
                            await session.StoreAsync(p, ct).ConfigureAwait(false);

                        await session.SaveChangesAsync(ct).ConfigureAwait(false);
                    }
                }, disposeMidfunc: downstream => () =>
                {
                    downstream();
                    foreach (var p in projectionGroups.Values)
                        p.Dispose();
                })
                .Build();
        }

        public AllStreamPosition GetStartingPosition() =>
            _observer.Min;

        public void Dispose()
        {
        }
    }
}