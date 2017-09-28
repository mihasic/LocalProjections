namespace LocalProjections
{
    using System;

    internal class DelegateDisposable : IDisposable
    {
        private readonly Action _dispose;

        public DelegateDisposable(Action dispose) =>
            _dispose = dispose;

        public void Dispose() =>
            _dispose();
    }
}
