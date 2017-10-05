namespace LocalProjections.RavenDb.XTests
{
    public class RavenDocument
    {
        public string Id { get; set; }
        public string LookupId { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string[] Children { get; set; }

        public static string GetId(string name) =>
            nameof(RavenDocument) + "s/" + name;
    }
}