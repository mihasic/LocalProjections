namespace LocalProjections
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using LightningStore;
    using LuceneSearch;

    internal static class Extensions
    {
        public static async Task HandleException(this Func<Task> action, Action<Exception> onError = null)
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                onError?.Invoke(ex);
            }
        }
    }
}
