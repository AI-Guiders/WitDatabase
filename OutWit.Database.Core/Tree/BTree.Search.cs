namespace OutWit.Database.Core.Tree;

public sealed partial class BTree
{
    #region Search

    /// <summary>
    /// Searches for a key and returns its value, or null if not found.
    /// </summary>
    public byte[]? Search(ReadOnlySpan<byte> key)
    {
        ThrowIfDisposed();
        ValidateKey(key);
        
        var (leafPage, index, found) = FindLeafInfo(key);
        
        if (!found)
            return null;
        
        var page = m_pageManager.GetPage(leafPage);
        try
        {
            var node = new BTreeNode(page.Data, PageSize, leafPage);
            
            if (node.IsOverflowValue(index))
            {
                uint overflowPage = node.GetOverflowPage(index);
                m_pageManager.ReleasePage(leafPage);
                page = null!; // Mark as released to avoid double-release in finally
                return m_pageManagerOverflowManager.ReadOverflow(overflowPage);
            }
            
            return node.GetValue(index).ToArray();
        }
        finally
        {
            if (page != null!)
            {
                m_pageManager.ReleasePage(leafPage);
            }
        }
    }

    /// <summary>
    /// Searches for a key asynchronously and returns its value, or null if not found.
    /// Use this in environments where synchronous I/O is not available (e.g., Blazor WASM).
    /// </summary>
    /// <param name="key">The key to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The value if found, or null.</returns>
    public async ValueTask<byte[]?> SearchAsync(ReadOnlyMemory<byte> key, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateKey(key.Span);
        
        var (leafPage, index, found) = await FindLeafInfoAsync(key, cancellationToken).ConfigureAwait(false);
        
        if (!found)
            return null;
        
        var page = await m_pageManager.GetPageAsync(leafPage, cancellationToken).ConfigureAwait(false);
        try
        {
            var node = new BTreeNode(page.Data, PageSize, leafPage);
            
            if (node.IsOverflowValue(index))
            {
                uint overflowPage = node.GetOverflowPage(index);
                m_pageManager.ReleasePage(leafPage);
                page = null!;
                return await m_pageManagerOverflowManager.ReadOverflowAsync(overflowPage, cancellationToken).ConfigureAwait(false);
            }
            
            return node.GetValue(index).ToArray();
        }
        finally
        {
            if (page != null!)
            {
                m_pageManager.ReleasePage(leafPage);
            }
        }
    }

    /// <summary>
    /// Checks if a key exists in the tree.
    /// </summary>
    public bool ContainsKey(ReadOnlySpan<byte> key)
    {
        ThrowIfDisposed();
        ValidateKey(key);
        
        var (_, _, found) = FindLeafInfo(key);
        return found;
    }

    /// <summary>
    /// Checks if a key exists in the tree asynchronously.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the key exists.</returns>
    public async ValueTask<bool> ContainsKeyAsync(ReadOnlyMemory<byte> key, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateKey(key.Span);
        
        var (_, _, found) = await FindLeafInfoAsync(key, cancellationToken).ConfigureAwait(false);
        return found;
    }

    /// <summary>
    /// Finds the leaf page info that should contain the key.
    /// </summary>
    private (uint PageNumber, int Index, bool Found) FindLeafInfo(ReadOnlySpan<byte> key)
    {
        uint currentPage = m_rootPageNumber;
        
        while (true)
        {
            var page = m_pageManager.GetPage(currentPage);
            var node = new BTreeNode(page.Data, PageSize, currentPage);
            
            if (node.IsLeaf)
            {
                int index = node.SearchKey(key);
                bool found = index >= 0;
                m_pageManager.ReleasePage(currentPage);
                return (currentPage, found ? index : ~index, found);
            }
            
            int childIndex = node.FindChildIndex(key);
            uint childPage = childIndex < node.KeyCount 
                ? node.GetChild(childIndex) 
                : node.RightmostChild;
            
            m_pageManager.ReleasePage(currentPage);
            currentPage = childPage;
        }
    }

    /// <summary>
    /// Finds the leaf page info that should contain the key asynchronously.
    /// </summary>
    private async ValueTask<(uint PageNumber, int Index, bool Found)> FindLeafInfoAsync(
        ReadOnlyMemory<byte> key, CancellationToken cancellationToken)
    {
        uint currentPage = m_rootPageNumber;
        
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var page = await m_pageManager.GetPageAsync(currentPage, cancellationToken).ConfigureAwait(false);
            var node = new BTreeNode(page.Data, PageSize, currentPage);
            
            if (node.IsLeaf)
            {
                int index = node.SearchKey(key.Span);
                bool found = index >= 0;
                m_pageManager.ReleasePage(currentPage);
                return (currentPage, found ? index : ~index, found);
            }
            
            int childIndex = node.FindChildIndex(key.Span);
            uint childPage = childIndex < node.KeyCount 
                ? node.GetChild(childIndex) 
                : node.RightmostChild;
            
            m_pageManager.ReleasePage(currentPage);
            currentPage = childPage;
        }
    }

    /// <summary>
    /// Finds the leftmost leaf page in the tree.
    /// </summary>
    private uint FindLeftmostLeaf()
    {
        uint currentPage = m_rootPageNumber;
        
        while (true)
        {
            var page = m_pageManager.GetPage(currentPage);
            var node = new BTreeNode(page.Data, PageSize, currentPage);
            
            if (node.IsLeaf)
            {
                m_pageManager.ReleasePage(currentPage);
                return currentPage;
            }
            
            uint childPage = node.GetChild(0);
            m_pageManager.ReleasePage(currentPage);
            currentPage = childPage;
        }
    }

    /// <summary>
    /// Finds the leftmost leaf page in the tree asynchronously.
    /// </summary>
    private async ValueTask<uint> FindLeftmostLeafAsync(CancellationToken cancellationToken)
    {
        uint currentPage = m_rootPageNumber;
        
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var page = await m_pageManager.GetPageAsync(currentPage, cancellationToken).ConfigureAwait(false);
            var node = new BTreeNode(page.Data, PageSize, currentPage);
            
            if (node.IsLeaf)
            {
                m_pageManager.ReleasePage(currentPage);
                return currentPage;
            }
            
            uint childPage = node.GetChild(0);
            m_pageManager.ReleasePage(currentPage);
            currentPage = childPage;
        }
    }

    #endregion
}
