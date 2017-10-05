namespace LocalProjections.RavenDb.Tests
{
    using System.Linq;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;
    public class SearchDocumentIndex : AbstractIndexCreationTask<RavenDocument, SearchDocument>
    {
        public SearchDocumentIndex()
        {
            Map = docs =>
                from doc in docs
                let lookup = LoadDocument<ParentLookup>(doc.LookupId)
                select new SearchDocument
                {
                    Name = doc.Name,
                    Parents = lookup.Parents.ToArray(),
                    Children = doc.Children,
                    Value = doc.Value
                };
            Index(x => x.Value, FieldIndexing.Analyzed);
            StoreAllFields(FieldStorage.Yes);
        }
    }
}