namespace OutWit.Database.Core.Concurrency
{
    /// <summary>
    /// Synchronous lock handle.
    /// </summary>
    internal sealed class LockHandleSync : IDisposable
    {
        #region Fields

        private Action? m_releaseAction;

        #endregion

        #region Constructors

        public LockHandleSync(Action releaseAction)
        {
            m_releaseAction = releaseAction;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            var action = Interlocked.Exchange(ref m_releaseAction, null);
            action?.Invoke();
        }

        #endregion
    }
}
