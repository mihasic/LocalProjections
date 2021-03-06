namespace LocalProjections
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using LightningStore;

    using static Extensions;

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
                (m, ct) => projection.Project(m, ct),
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

        public static IStatefulProjection Bind<TKey, TValue>(
            Func<Lazy<CachingRepository<TKey, TValue>>> createSession,
            Func<Lazy<CachingRepository<TKey, TValue>>, Func<Envelope, CancellationToken, Task>> projectBuilder)
            where TValue : new()
        {
            var session = createSession();
            var projection = projectBuilder(session);
            return new DelegateProjection(
                (m, ct) => projection(m, ct),
                ct =>
                {
                    if (session.IsValueCreated)
                    {
                        using (var s = session.Value)
                        {
                            s.Commit();
                            session = createSession();
                        }
                        projection = projectBuilder(session);
                    }
                    return Task.CompletedTask;
                },
                () =>
                {
                    if (session.IsValueCreated)
                        session.Value.Dispose();
                }
            );
        }

        public static IStatefulProjection Combine(params IStatefulProjection[] projections) =>
            new DelegateProjection(
                (m, ct) => ForEach(projections, p => p.Project(m, ct), ct),
                ct => ForEach(projections, p => p.Commit(ct), ct),
                () => ForEach(projections, p => p.Dispose())
            );

        public static IStatefulProjection Combine(IStatefulProjection projection,
            Action<Envelope> project = null,
            Action commit = null) =>
            Combine(projection, new DelegateProjection((m, ct) => Async(() => project?.Invoke(m)), _ => Async(commit)));

        private static async Task ForEach<T>(IEnumerable<T> instances, Func<T, Task> action, CancellationToken ct)
        {
            foreach (var instance in instances)
            {
                if (ct.IsCancellationRequested)
                    return;
                await action(instance).ConfigureAwait(false);
            }
        }

        private static void ForEach<T>(IEnumerable<T> instances, Action<T> action)
        {
            foreach (var instance in instances)
            {
                action(instance);
            }
        }
    }
}
