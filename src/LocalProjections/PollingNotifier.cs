namespace LocalProjections
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Nito.AsyncEx;

    public class PollingNotifier : IDisposable
    {
        private readonly Func<CancellationToken, Task<AllStreamPosition>> _getHeadPosition;
        private readonly Func<Exception, AllStreamPosition, Task> _onError;
        private readonly TimeSpan _interval;
        private readonly AsyncAutoResetEvent _streamStoreNotification = new AsyncAutoResetEvent();
        private readonly CancellationTokenSource _disposed = new CancellationTokenSource();

        public PollingNotifier(
            Func<CancellationToken, Task<AllStreamPosition>> getHeadPosition,
            Func<Exception, AllStreamPosition, Task> onError = null,
            int interval = 1000)
        {
            _getHeadPosition = getHeadPosition;
            _onError = onError ?? ((_, __) => Task.CompletedTask);
            _interval = TimeSpan.FromMilliseconds(interval);
            Task.Run(Poll, _disposed.Token);
        }

        private async Task Poll()
        {
            var headPosition = AllStreamPosition.None;
            var previousHeadPosition = headPosition;

            while(!_disposed.IsCancellationRequested)
            {
                try
                {
                    headPosition = await _getHeadPosition(_disposed.Token).ConfigureAwait(false);
                    System.Diagnostics.Debug.WriteLine($"### Position {headPosition}");
                }
                catch(Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"### Error {ex}");
                    await _onError(ex, headPosition).ConfigureAwait(false);
                }

                if(headPosition > previousHeadPosition)
                {
                    _streamStoreNotification.Set();
                    System.Diagnostics.Debug.WriteLine($"### Set notification {headPosition}");
                    previousHeadPosition = headPosition;
                }
                else
                {
                    await Task.Delay(_interval, _disposed.Token);
                }
            }
        }

        public Task WaitForNotification() =>
            _streamStoreNotification.WaitAsync(_disposed.Token);

        public void Dispose() =>
            _disposed.Cancel();
     }
}