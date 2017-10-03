namespace LocalProjections
{
    using System;
    using LightningStore;

    public static class CachingRepositoryHelper
    {
        public static Lazy<CachingRepository<TKey, TDocument>> CreateSession<TKey, TDocument>(
            ObjectRepository<TKey, TDocument> repo,
            SearchProjector<TKey, TDocument> searchProjector) where TDocument : new() =>
            new Lazy<CachingRepository<TKey, TDocument>>(() =>
                new CachingRepository<TKey, TDocument>(
                    repo,
                    searchProjector.ProcessDeletes,
                    ds => searchProjector.ProcessUpserts(ds)));
        public static Lazy<CachingRepository<TKey, TDocument>> CreateSession<TKey, TDocument>(
            ObjectRepository<TKey, TDocument> repo) where TDocument : new() =>
            new Lazy<CachingRepository<TKey, TDocument>>(() =>
                new CachingRepository<TKey, TDocument>(repo));
    }
}
