namespace LocalProjections
{
    using System.Collections.Generic;

    public interface IProjectionGroupStateObserver
    {
        ProjectionGroupState this[string name] { get; }
        IReadOnlyDictionary<string, ProjectionGroupState> All { get; }
        IEnumerable<string> Suspended { get; }
        IEnumerable<string> Active { get; }
        AllStreamPosition Min { get; }
        AllStreamPosition Max { get; }
    }
}
