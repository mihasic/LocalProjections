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
        private readonly Func<TDocument, bool> _predicate;

        public SearchProjector(
            Index index,
            string idTermName,
            Func<TKey, TDocument, IEnumerable<KeyValuePair<string, string>>> extractDocument,
            Func<TDocument, bool> predicate = null)
        {
            _index = index;
            _idTermName = idTermName;
            _extractDocument = extractDocument;
            _predicate = predicate ?? (_ => true);
        }

        public void ProcessDeletes(IEnumerable<TKey> deletes)
        {
            foreach (var delete in deletes)
                _index.DeleteByTerm(_idTermName, delete.ToString(), false);
        }

        public void ProcessUpserts(IEnumerable<KeyValuePair<TKey, TDocument>> documents)
        {
            foreach (var document in documents)
            {
                if (_predicate(document.Value))
                    _index.UpdateByTerm(_idTermName, document.Key.ToString(),
                        _extractDocument(document.Key, document.Value), false);
                else
                    _index.DeleteByTerm(_idTermName, document.Key.ToString(), false);
            }
        }

        public void Commit() =>
            _index.Commit();
    }
}
