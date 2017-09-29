namespace LocalProjections
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using LightningStore;

    public static class ProjectionHelpers
    {
        public static IStatefulProjection Bind<TKey, TValue>(
            Func<Lazy<CachingRepository<TKey, TValue>>> createSession,
            Func<Lazy<CachingRepository<TKey, TValue>>, IStatefulProjection> projectBuilder)
            where TValue : new()
        {
            var session = createSession();
            var projection = projectBuilder(session);
            return new DelegateProjection(
                projection.Project,
                async ct =>
                {
                    await projection.Commit(ct).ConfigureAwait(false);

                    if (session.IsValueCreated)
                    {
                        using (var s = session.Value)
                        {
                            s.Commit();
                            session = createSession();
                        }
                        projection = projectBuilder(session);
                    }
                },
                () =>
                {
                    projection.Dispose();
                    if (session.IsValueCreated)
                        session.Value.Dispose();
                }
            );
        }
    }
}
