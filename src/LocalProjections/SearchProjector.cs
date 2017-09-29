namespace LocalProjections
{
    using System;
    using System.Collections.Generic;
    using LuceneSearch;

    public class SearchProjector<TKey, TDocument>
    {
        private readonly Index _index;
        private readonly string _idTermName;
        private readonly Func<TKey, TDocument, IEnumerable<KeyValuePair<string, string>>> _extractDocument;

        public SearchProjector(
            Index index,
            string idTermName,
            Func<TKey, TDocument, IEnumerable<KeyValuePair<string, string>>> extractDocument)
        {
            _index = index;
            _idTermName = idTermName;
            _extractDocument = extractDocument;
        }

        public void ProcessDeletes(IEnumerable<TKey> deletes)
        {
            foreach (var delete in deletes)
                _index.DeleteByTerm(_idTermName, delete.ToString(), false);
        }

        public void ProcessUpserts(IEnumerable<KeyValuePair<TKey, TDocument>> documents)
        {
            foreach (var document in documents)
                _index.UpdateByTerm(_idTermName, document.Key.ToString(),
                    _extractDocument(document.Key, document.Value), false);
        }

        public void Commit() =>
            _index.Commit();
    }
}
