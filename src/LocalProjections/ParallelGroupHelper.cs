namespace LocalProjections
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public static class ParallelGroupHelper
    {
        public static async Task<IStatefulProjection> CreateObservableParallelGroup(
            Func<Task<IReadOnlyDictionary<string, IStatefulProjection>>> buildProjectionGroups,
            ProjectionGroupStateObserver observer,
            Action notifyRestart = null)
        {
            observer.Reset();
            var projectionGroups = await buildProjectionGroups().ConfigureAwait(false);

            Func<IReadOnlyCollection<IStatefulProjection>> filtered =
                () => observer.Active.Select(x => projectionGroups[x]).ToArray();
            var host = new ParallelExecutionHost(filtered);

            return new StatefulProjectionBuilder(host)
                .Use(commitMidfunc: downstream => async ct =>
                {
                    await downstream(ct).ConfigureAwait(false);
                    if (notifyRestart != null && observer.All.Any() && !observer.Active.Any())
                        notifyRestart();
                }, disposeMidfunc: downstream => () =>
                {
                    downstream();
                    foreach (var p in projectionGroups.Values)
                        p.Dispose();
                })
                .Build();
        }
    }
}
