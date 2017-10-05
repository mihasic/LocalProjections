namespace LocalProjections.RavenDb.XTests
{
    using Raven.Client;
    using Raven.Client.Connection.Async;
    using Raven.Client.Document;

    internal static class AsyncDocumentSessionExtensions
    {
        public static string GetDatabaseName(this IAsyncDocumentSession session)
        {
            return ((InMemoryDocumentSessionOperations)session).DatabaseName;
        }

        public static IAsyncDatabaseCommands GetAsyncDatabaseCommands(this IAsyncDocumentSession session)
        {
            var databaseCommands = session.Advanced.DocumentStore.AsyncDatabaseCommands;

            var databaseName = session.GetDatabaseName();

            return databaseName == null ? databaseCommands : databaseCommands.ForDatabase(databaseName);
        }
    }
}