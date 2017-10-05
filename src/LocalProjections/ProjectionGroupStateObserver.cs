namespace LocalProjections
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    public class ProjectionGroupStateObserver : IProjectionGroupStateObserver
    {
        private readonly ConcurrentDictionary<string, ProjectionGroupState> _states =
            new ConcurrentDictionary<string, ProjectionGroupState>();

        public ProjectionGroupState this[string name]
        {
            get { return _states.GetOrAdd(name, new ProjectionGroupState(name)); }
            set { _states[name] = value; }
        }

        public void MoveTo(string name, AllStreamPosition checkpoint) =>
            this[name] = this[name].MoveTo(checkpoint);
        public void Suspend(string name, Exception ex) =>
            this[name] = this[name].Suspend(ex);

        public void Reset() =>
            _states.Clear();

        public IReadOnlyDictionary<string, ProjectionGroupState> All =>
            _states;

        public AllStreamPosition Min =>
            All.Values.Min(x => x.Checkpoint);

        public AllStreamPosition Max =>
            All.Values.Max(x => x.Checkpoint);

        public IEnumerable<string> Suspended =>
            All.Values.Where(x => x.Suspended).Select(x => x.Name);

        public IEnumerable<string> Active =>
            All.Values.Where(x => !x.Suspended).Select(x => x.Name);
    }
}
