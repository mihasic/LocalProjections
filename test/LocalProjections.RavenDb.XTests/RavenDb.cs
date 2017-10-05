namespace LocalProjections.RavenDb.XTests
{
    using System;
    using System.Threading;
    using Raven.Client;
    using Raven.Client.Document;
    using Raven.Client.Embedded;

    internal static class RavenDb
    {
        private static readonly Lazy<IDocumentStore> s_instance;

        static RavenDb()
        {
            s_instance = new Lazy<IDocumentStore>(() => new EmbeddableDocumentStore
            {
                RunInMemory = true,
                Configuration =
                {
                    RunInMemory = true,
                    RunInUnreliableYetFastModeThatIsNotSuitableForProduction = true
                }
            }.Initialize(), LazyThreadSafetyMode.ExecutionAndPublication);

            AppDomain.CurrentDomain.DomainUnload += (_, e) =>
            {
                if (s_instance.IsValueCreated)
                    s_instance.Value.Dispose();
            };
        }

        public static Func<IAsyncDocumentSession> CreateSessionFactory()
        {
            var databaseId = Guid.NewGuid().ToString("N");
            s_instance.Value.DatabaseCommands.GlobalAdmin.EnsureDatabaseExists(databaseId, true);
            return () => s_instance.Value.OpenAsyncSession(databaseId);
        }
    }
}