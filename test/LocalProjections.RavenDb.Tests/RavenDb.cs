namespace LocalProjections.RavenDb.Tests
{
    using System;
    using Raven.Client;
    using Raven.Client.Document;
    using Raven.Client.Embedded;

    internal static class RavenDb
    {
        private static readonly IDocumentStore s_instance;

        static RavenDb()
        {
            s_instance = new EmbeddableDocumentStore
            {
                RunInMemory = true,
                Configuration =
                {
                    RunInMemory = true,
                    RunInUnreliableYetFastModeThatIsNotSuitableForProduction = true
                }
            }.Initialize();

            // AppDomain.CurrentDomain.DomainUnload += (_, e) => s_instance.Dispose();
        }

        public static Func<IAsyncDocumentSession> CreateSessionFactory()
        {
            var databaseId = Guid.NewGuid().ToString("N");
            s_instance.DatabaseCommands.GlobalAdmin.EnsureDatabaseExists(databaseId, true);
            return () => s_instance.OpenAsyncSession(databaseId);
        }
    }
}