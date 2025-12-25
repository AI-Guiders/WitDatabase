namespace OutWit.Database.Core.Concurrency
{
    /// <summary>
    /// Combined synchronous handle that releases both process and file locks.
    /// </summary>
    internal sealed class LockHandleSyncCombined : IDisposable
    {
        #region Fields

        private IDisposable? m_processHandle;

        private FileLock? m_fileLock;

        #endregion

        #region Constructors

        public LockHandleSyncCombined(IDisposable processHandle, FileLock? fileLock)
        {
            m_processHandle = processHandle;
            m_fileLock = fileLock;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            var fileLock = Interlocked.Exchange(ref m_fileLock, null);
            fileLock?.ReleaseLock();

            var processHandle = Interlocked.Exchange(ref m_processHandle, null);
            processHandle?.Dispose();
        }

        #endregion
    }
}
