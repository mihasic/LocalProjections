namespace LocalProjections
{
    using System;

    public class ProjectionGroupState
    {
        public readonly string Name;
        public readonly bool Suspended;
        public readonly Exception Exception;
        public readonly AllStreamPosition Checkpoint;

        public ProjectionGroupState(
            string name,
            bool suspended = false,
            Exception exception = null,
            AllStreamPosition checkpoint = default(AllStreamPosition))
        {
            Name = name;
            Suspended = suspended;
            Exception = exception;
            Checkpoint = checkpoint;
        }

        public ProjectionGroupState Suspend(Exception exception) =>
            new ProjectionGroupState(Name, true, exception, Checkpoint);

        public ProjectionGroupState MoveTo(AllStreamPosition checkpoint) =>
            new ProjectionGroupState(Name, false, Exception, checkpoint);
    }
}
