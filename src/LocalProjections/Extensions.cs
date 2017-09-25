namespace LocalProjections
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using LightningStore;
    using LuceneSearch;

    public static class Extensions
    {
        public static async Task HandleException(this Func<Task> action, Action<Exception> onError)
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                onError(ex);
            }
        }

        public static CachingRepository<Guid, TDocument> CreateSearchAwareRepository<TDocument>(
            ObjectRepository<Guid, TDocument> repository,
            Index index,
            string idFieldName,
            Func<TDocument, IEnumerable<KeyValuePair<string, string>>> extractDocument,
            CancellationToken cancellationToken)
             where TDocument : new()
        {
            return new CachingRepository<Guid, TDocument>(
                repository,
                onCommittedDeletes: ds =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        foreach (var d in ds)
                            index.DeleteByTerm(idFieldName, d.ToString(), commit: false);
                    }
                },
                onCommittedUpserts: dict =>
                {
                    foreach (var p in dict)
                    {
                        if (cancellationToken.IsCancellationRequested) return;
                        var doc = extractDocument(p.Value);
                        index.UpdateByTerm(idFieldName, p.Key.ToString(), doc, commit: false);
                    }
                    index.Commit();
                });
        }
    }
}
