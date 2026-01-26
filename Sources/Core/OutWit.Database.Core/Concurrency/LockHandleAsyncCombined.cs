namespace OutWit.Database.Core.Concurrency
{
    /// <summary>
    /// Combined async handle that releases both process and file locks.
    /// </summary>
    internal sealed class LockHandleAsyncCombined : IAsyncDisposable
    {
        #region Fields

        private IAsyncDisposable? m_processHandle;

        private FileLock? m_fileLock;

        #endregion

        #region Constructors

        public LockHandleAsyncCombined(IAsyncDisposable processHandle, FileLock? fileLock)
        {
            m_processHandle = processHandle;
            m_fileLock = fileLock;
        }

        #endregion

        #region IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            var fileLock = Interlocked.Exchange(ref m_fileLock, null);
            fileLock?.ReleaseLock();

            var processHandle = Interlocked.Exchange(ref m_processHandle, null);
            if (processHandle != null)
            {
                await processHandle.DisposeAsync().ConfigureAwait(false);
            }
        }

        #endregion
    }
}
