namespace LocalProjections.RavenDb.Tests
{
    public class ProjectionPosition
    {
        public string Id { get; set; }
        public long? Position { get; set; }

        public static string GetIdFromName(string name) =>
            nameof(ProjectionPosition) + "s/" + name;
    }
}