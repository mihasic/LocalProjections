namespace LocalProjections
{
    using System;
    using System.Threading.Tasks;

    public static class Extensions
    {
        internal static async Task HandleException(this Func<Task> action, Action<Exception> onError = null)
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

        internal static Task Async(Action action)
        {
            action?.Invoke();
            return Task.CompletedTask;
        }
    }
}
