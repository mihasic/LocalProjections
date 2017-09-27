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
        private readonly AsyncAutoResetEvent _autoResetEvent = new AsyncAutoResetEvent();
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
                }
                catch(Exception ex)
                {
                    await Extensions.HandleException(() => _onError(ex, headPosition)).ConfigureAwait(false);
                }

                if(headPosition > previousHeadPosition)
                {
                    _autoResetEvent.Set();
                    previousHeadPosition = headPosition;
                }
                else
                {
                    await Task.Delay(_interval, _disposed.Token);
                }
            }
        }

        public async Task WaitForNotification(CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposed.Token))
            {
                await _autoResetEvent.WaitAsync(cts.Token);
            }
        }

        public void Dispose() =>
            _disposed.Cancel();
     }
}