namespace LocalProjections.Tests
{
    using System;
    using System.Threading.Tasks;
    using Shouldly;
    using Xunit;
    using Xunit.Abstractions;

    public class PollingNotifierTests : IDisposable
    {
        private AllStreamPosition _currentPosition;
        private readonly PollingNotifier _notifier;
        private const int Interval = 300;
        private readonly TaskCompletionSource<Exception> _error = new TaskCompletionSource<Exception>();

        public PollingNotifierTests()
        {
            _currentPosition = AllStreamPosition.None;
            _notifier = new PollingNotifier(
                _ => _currentPosition.ToInt64() != 9999
                    ? Task.FromResult(_currentPosition)
                    : throw new InvalidOperationException("Error happened"),
                (ex, p) =>
                {
                    _error.SetResult(ex);
                    return Task.CompletedTask;
                },
                interval: Interval);
        }

        public void Dispose() => _notifier.Dispose();

        private Task<bool> Wait() =>
            _notifier.WaitForNotification().WithTimeout(Interval*3);

        [Fact]
        public async Task No_notification_on_null_position()
        {
            var result = await Wait();
            result.ShouldBeFalse();
        }

        [Fact]
        public async Task Notification_on_position_change()
        {
            _currentPosition = new AllStreamPosition(_currentPosition.ToInt64() + 1);
            var result = await Wait();
            result.ShouldBeTrue();
        }

        [Fact]
        public async Task Recovers_on_error()
        {
            _currentPosition = new AllStreamPosition(9998);
            (await Wait()).ShouldBeTrue();

            _currentPosition = new AllStreamPosition(_currentPosition.ToInt64() + 1);
            var error = await _error.Task.WithTimeout<Exception>(Interval*3);
            error.ShouldBeOfType<InvalidOperationException>();

            _currentPosition = new AllStreamPosition(_currentPosition.ToInt64() + 1);
            (await Wait()).ShouldBeTrue();
        }
    }
}