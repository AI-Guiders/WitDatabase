namespace OutWit.Database.Core.Tree;

public sealed partial class BTree
{
    #region Delete

    /// <summary>
    /// Deletes a key from the tree.
    /// </summary>
    public bool Delete(ReadOnlySpan<byte> key)
    {
        ThrowIfDisposed();
        ValidateKey(key);
        
        var (leafPage, index, found) = FindLeafInfo(key);
        
        if (!found)
            return false;
        
        var page = m_pageManager.GetPage(leafPage);
        try
        {
            var node = new BTreeNode(page.Data, PageSize, leafPage);
            
            // Free overflow if exists
            if (node.IsOverflowValue(index))
            {
                uint overflowPage = node.GetOverflowPage(index);
                m_pageManager.ReleasePage(leafPage);
                page = null!; // Mark as released
                
                m_pageManagerOverflowManager.FreeOverflow(overflowPage);
                
                page = m_pageManager.GetPage(leafPage);
                node = new BTreeNode(page.Data, PageSize, leafPage);
            }
            
            node.RemoveAt(index);
            page.MarkDirty();
            
            m_entryCount--;
            m_entryCountDirty = true;
            
            return true;
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
    /// Deletes a key from the tree asynchronously.
    /// Use this in environments where synchronous I/O is not available (e.g., Blazor WASM).
    /// </summary>
    /// <param name="key">The key to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the key was deleted, false if not found.</returns>
    public async ValueTask<bool> DeleteAsync(ReadOnlyMemory<byte> key, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateKey(key.Span);
        
        var (leafPage, index, found) = await FindLeafInfoAsync(key, cancellationToken).ConfigureAwait(false);
        
        if (!found)
            return false;
        
        var page = await m_pageManager.GetPageAsync(leafPage, cancellationToken).ConfigureAwait(false);
        try
        {
            var node = new BTreeNode(page.Data, PageSize, leafPage);
            
            // Free overflow if exists
            if (node.IsOverflowValue(index))
            {
                uint overflowPage = node.GetOverflowPage(index);
                m_pageManager.ReleasePage(leafPage);
                page = null!;
                
                await m_pageManagerOverflowManager.FreeOverflowAsync(overflowPage, cancellationToken).ConfigureAwait(false);
                
                page = await m_pageManager.GetPageAsync(leafPage, cancellationToken).ConfigureAwait(false);
                node = new BTreeNode(page.Data, PageSize, leafPage);
            }
            
            node.RemoveAt(index);
            page.MarkDirty();
            
            m_entryCount--;
            m_entryCountDirty = true;
            
            return true;
        }
        finally
        {
            if (page != null!)
            {
                m_pageManager.ReleasePage(leafPage);
            }
        }
    }

    #endregion
}
