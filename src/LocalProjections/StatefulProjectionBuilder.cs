namespace LocalProjections
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using ProjectFunc = System.Func<Envelope, System.Threading.CancellationToken, System.Threading.Tasks.Task>;
    using CommitFunc = System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task>;

    public class StatefulProjectionBuilder
    {
        private Func<IStatefulProjection> _projection;

        public StatefulProjectionBuilder(IStatefulProjection projection = null) =>
            _projection = () => projection ?? new DelegateProjection();

        public StatefulProjectionBuilder(Func<IStatefulProjection> projectionBuilder) =>
            _projection = projectionBuilder;

        public StatefulProjectionBuilder Use(Func<IStatefulProjection, IStatefulProjection> projection)
        {
            var current = _projection;
            _projection = () => projection(current());
            return this;
        }

        public StatefulProjectionBuilder UseAfter(IStatefulProjection projection) =>
            Use(next => ProjectionHelpers.Combine(projection, next));

        public StatefulProjectionBuilder UseBefore(IStatefulProjection projection) =>
            Use(next => ProjectionHelpers.Combine(projection, next));
        
        public StatefulProjectionBuilder Use(
            Func<ProjectFunc, ProjectFunc> projectMidfunc = null,
            Func<CommitFunc, CommitFunc> commitMidfunc = null,
            Func<Action, Action> disposeMidfunc = null) =>
            Use(next => new DelegateProjection(
                project: projectMidfunc?.Invoke(next.Project) ?? next.Project,
                commit: commitMidfunc?.Invoke(next.Commit) ?? next.Commit,
                dispose: disposeMidfunc?.Invoke(next.Dispose) ?? next.Dispose));

        public IStatefulProjection Build() =>
            _projection();
        public Func<IStatefulProjection> BuildFactory() =>
            _projection;
    }
}
