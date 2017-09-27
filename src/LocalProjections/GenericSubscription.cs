namespace LocalProjections
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class GenericSubscription : IDisposable
    {
        private readonly ReadAllPageFunc _readAllPage;
        private readonly CancellationTokenSource _disposed = new CancellationTokenSource();
        private readonly Func<CancellationToken, Task> _waitForEvent;
        private readonly TaskCompletionSource<object> _started = new TaskCompletionSource<object>();
        private readonly MessageReceived _onMessage;
        private readonly HasCaughtUp _hasCaughtUp;
        private readonly Func<Exception, Task> _onSubscriptionError;

        private AllStreamPosition _nextPosition;

        public GenericSubscription(
            ReadAllPageFunc readAllPage,
            AllStreamPosition fromPosition,
            Func<CancellationToken, Task> waitForEvent,
            MessageReceived onMessage,
            Func<Exception, Task> onSubscriptionError,
            HasCaughtUp hasCaughtUp)
        {
            _readAllPage = readAllPage;
            FromPosition = fromPosition;
            _nextPosition = new AllStreamPosition(fromPosition.ToNullableInt64() + 1 ?? 0);
            _waitForEvent = waitForEvent;
            _onMessage = onMessage;
            _hasCaughtUp = hasCaughtUp ?? (() => Task.CompletedTask);
            _onSubscriptionError = onSubscriptionError ?? (_ => Task.CompletedTask);
            LastPosition = fromPosition;

            Task.Run(PullAndPush);
        }

        public long? FromPosition { get; }
        public long? LastPosition { get; private set; }
        public Task Started => _started.Task;

        private async Task PullAndPush()
        {
            _started.SetResult(null);

            while (true)
            {
                bool pause = false;

                while (!pause)
                {
                    var page = await Pull();

                    await Push(page);

                    if (page.IsEnd && page.Messages.Count > 0)
                    {
                        await _hasCaughtUp().ConfigureAwait(false);
                    }

                    pause = page.IsEnd && page.Messages.Count == 0;
                }

                // Wait for notification before starting again. 
                await _waitForEvent(_disposed.Token).ConfigureAwait(false);
            }
        }

        private async Task<ReadAllPage> Pull()
        {
            ReadAllPage readAllPage;
            try
            {
                readAllPage = await _readAllPage(_nextPosition, _disposed.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException) && !(ex is ObjectDisposedException))
            {
                await _onSubscriptionError(ex).ConfigureAwait(false);
                throw;
            }
            return readAllPage;
        }

        private async Task Push(ReadAllPage page)
        {
            foreach (var message in page.Messages)
            {
                if (_disposed.IsCancellationRequested)
                {
                    _disposed.Token.ThrowIfCancellationRequested();
                }
                _nextPosition = new AllStreamPosition(message.Checkpoint.ToInt64() + 1);
                LastPosition = message.Checkpoint;
                try
                {
                    await _onMessage(this, message, _disposed.Token).ConfigureAwait(false);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    await _onSubscriptionError(ex).ConfigureAwait(false);
                    throw;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed.IsCancellationRequested)
                return;

            _disposed.Cancel();
        }
     }
}