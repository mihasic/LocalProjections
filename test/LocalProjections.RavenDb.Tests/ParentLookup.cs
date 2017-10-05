namespace LocalProjections.RavenDb.Tests
{
    using System.Collections.Generic;

    public class ParentLookup
    {
        public string Id { get; set; }
        public string DocumentId { get; set; }
        public List<string> Parents { get; set; }

        public static string GetId(string name) =>
            nameof(ParentLookup) + "s/" + name;
    }
}