namespace LocalProjections
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    public class ProjectionGroupStateObserver
    {
        private readonly ConcurrentDictionary<string, ProjectionGroupState> _states = new ConcurrentDictionary<string, ProjectionGroupState>();

        public ProjectionGroupState this[string name]
        {
            get { return _states.GetOrAdd(name, new ProjectionGroupState(name)); }
            set { _states[name] = value; }
        }

        public IReadOnlyDictionary<string, ProjectionGroupState> All =>
            _states;

        public AllStreamPosition Min =>
            All.Values.Min(x => x.Checkpoint);

        public IEnumerable<string> Suspended =>
            All.Values.Where(x => x.Suspended).Select(x => x.Name);

        public IEnumerable<string> Active =>
            All.Values.Where(x => !x.Suspended).Select(x => x.Name);
    }
}
