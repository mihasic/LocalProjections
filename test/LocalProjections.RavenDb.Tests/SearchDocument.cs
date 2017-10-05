namespace LocalProjections.RavenDb.Tests
{
    public class SearchDocument
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string[] Children { get; set; }
        public string[] Parents { get; set; }
    }
}