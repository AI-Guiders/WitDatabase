namespace OutWit.Database.Core.Concurrency
{
    /// <summary>
    /// Asynchronous lock handle.
    /// </summary>
    internal sealed class LockHandleAsync : IAsyncDisposable
    {
        #region Fields

        private Func<ValueTask>? m_releaseFunc;

        #endregion

        #region Constructors

        public LockHandleAsync(Func<ValueTask> releaseFunc)
        {
            m_releaseFunc = releaseFunc;
        }

        #endregion

        #region IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            var func = Interlocked.Exchange(ref m_releaseFunc, null);
            if (func != null)
            {
                await func().ConfigureAwait(false);
            }
        }

        #endregion
    }
}
