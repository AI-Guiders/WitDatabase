using System.Runtime.CompilerServices;

namespace OutWit.Database.Core.Tree;

public sealed partial class BTree
{
    #region Range Scan

    /// <summary>
    /// Returns all key-value pairs in order.
    /// </summary>
    public IEnumerable<(byte[] Key, byte[] Value)> GetAll()
    {
        return GetRange(null, null);
    }

    /// <summary>
    /// Returns key-value pairs in the specified range.
    /// </summary>
    /// <param name="minKey">Start of range (inclusive), or null for beginning.</param>
    /// <param name="maxKey">End of range (exclusive), or null for end.</param>
    public IEnumerable<(byte[] Key, byte[] Value)> GetRange(byte[]? minKey, byte[]? maxKey)
    {
        ThrowIfDisposed();
        
        uint currentPage;
        int startIndex;
        
        if (minKey == null)
        {
            currentPage = FindLeftmostLeaf();
            startIndex = 0;
        }
        else
        {
            var (leafPage, index, _) = FindLeafInfo(minKey);
            currentPage = leafPage;
            startIndex = index;
        }
        
        while (currentPage != 0)
        {
            var pageResults = CollectPageEntries(currentPage, startIndex, maxKey, exclusive: true, out uint nextLeaf, out bool done);
            
            foreach (var item in pageResults)
                yield return item;
            
            if (done)
                yield break;
            
            currentPage = nextLeaf;
            startIndex = 0;
        }
    }

    /// <summary>
    /// Returns key-value pairs in the specified range (inclusive end).
    /// </summary>
    public IEnumerable<(byte[] Key, byte[] Value)> GetRangeInclusive(byte[]? minKey, byte[]? maxKey)
    {
        ThrowIfDisposed();
        
        uint currentPage;
        int startIndex;
        
        if (minKey == null)
        {
            currentPage = FindLeftmostLeaf();
            startIndex = 0;
        }
        else
        {
            var (leafPage, index, _) = FindLeafInfo(minKey);
            currentPage = leafPage;
            startIndex = index;
        }
        
        while (currentPage != 0)
        {
            var pageResults = CollectPageEntries(currentPage, startIndex, maxKey, exclusive: false, out uint nextLeaf, out bool done);
            
            foreach (var item in pageResults)
                yield return item;
            
            if (done)
                yield break;
            
            currentPage = nextLeaf;
            startIndex = 0;
        }
    }

    /// <summary>
    /// Returns all key-value pairs in order asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public IAsyncEnumerable<(byte[] Key, byte[] Value)> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return GetRangeAsync(null, null, cancellationToken);
    }

    /// <summary>
    /// Returns key-value pairs in the specified range asynchronously.
    /// Use this in environments where synchronous I/O is not available (e.g., Blazor WASM).
    /// </summary>
    /// <param name="minKey">Start of range (inclusive), or null for beginning.</param>
    /// <param name="maxKey">End of range (exclusive), or null for end.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async IAsyncEnumerable<(byte[] Key, byte[] Value)> GetRangeAsync(
        byte[]? minKey, 
        byte[]? maxKey,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        uint currentPage;
        int startIndex;
        
        if (minKey == null)
        {
            currentPage = await FindLeftmostLeafAsync(cancellationToken).ConfigureAwait(false);
            startIndex = 0;
        }
        else
        {
            var (leafPage, index, _) = await FindLeafInfoAsync(minKey, cancellationToken).ConfigureAwait(false);
            currentPage = leafPage;
            startIndex = index;
        }
        
        while (currentPage != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var (pageResults, nextLeaf, done) = await CollectPageEntriesAsync(
                currentPage, startIndex, maxKey, exclusive: true, cancellationToken).ConfigureAwait(false);
            
            foreach (var item in pageResults)
                yield return item;
            
            if (done)
                yield break;
            
            currentPage = nextLeaf;
            startIndex = 0;
        }
    }

    /// <summary>
    /// Returns key-value pairs in the specified range (inclusive end) asynchronously.
    /// </summary>
    /// <param name="minKey">Start of range (inclusive), or null for beginning.</param>
    /// <param name="maxKey">End of range (inclusive), or null for end.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async IAsyncEnumerable<(byte[] Key, byte[] Value)> GetRangeInclusiveAsync(
        byte[]? minKey, 
        byte[]? maxKey,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        uint currentPage;
        int startIndex;
        
        if (minKey == null)
        {
            currentPage = await FindLeftmostLeafAsync(cancellationToken).ConfigureAwait(false);
            startIndex = 0;
        }
        else
        {
            var (leafPage, index, _) = await FindLeafInfoAsync(minKey, cancellationToken).ConfigureAwait(false);
            currentPage = leafPage;
            startIndex = index;
        }
        
        while (currentPage != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var (pageResults, nextLeaf, done) = await CollectPageEntriesAsync(
                currentPage, startIndex, maxKey, exclusive: false, cancellationToken).ConfigureAwait(false);
            
            foreach (var item in pageResults)
                yield return item;
            
            if (done)
                yield break;
            
            currentPage = nextLeaf;
            startIndex = 0;
        }
    }

    /// <summary>
    /// Collects entries from a single leaf page without crossing yield boundary.
    /// </summary>
    private List<(byte[] Key, byte[] Value)> CollectPageEntries(
        uint pageNumber, int startIndex, byte[]? maxKey, bool exclusive,
        out uint nextLeaf, out bool reachedEnd)
    {
        var results = new List<(byte[] Key, byte[] Value)>();
        reachedEnd = false;
        nextLeaf = 0;
        
        var page = m_pageManager.GetPage(pageNumber);
        try
        {
            var node = new BTreeNode(page.Data, PageSize, pageNumber);
            nextLeaf = node.NextLeaf;
            int keyCount = node.KeyCount;
            
            for (int i = startIndex; i < keyCount; i++)
            {
                var keyBytes = node.GetKey(i).ToArray();
                
                // Check end boundary
                if (maxKey != null)
                {
                    int cmp = keyBytes.AsSpan().SequenceCompareTo(maxKey);
                    if (exclusive ? cmp >= 0 : cmp > 0)
                    {
                        reachedEnd = true;
                        break;
                    }
                }
                
                byte[] valueBytes;
                if (node.IsOverflowValue(i))
                {
                    uint overflowPage = node.GetOverflowPage(i);
                    m_pageManager.ReleasePage(pageNumber);
                    page = null!; // Mark as released
                    
                    valueBytes = m_pageManagerOverflowManager.ReadOverflow(overflowPage);
                    
                    page = m_pageManager.GetPage(pageNumber);
                    node = new BTreeNode(page.Data, PageSize, pageNumber);
                }
                else
                {
                    valueBytes = node.GetValue(i).ToArray();
                }
                
                results.Add((keyBytes, valueBytes));
            }
            
            return results;
        }
        finally
        {
            if (page != null!)
            {
                m_pageManager.ReleasePage(pageNumber);
            }
        }
    }

    /// <summary>
    /// Collects entries from a single leaf page asynchronously.
    /// </summary>
    private async ValueTask<(List<(byte[] Key, byte[] Value)> Results, uint NextLeaf, bool ReachedEnd)> CollectPageEntriesAsync(
        uint pageNumber, int startIndex, byte[]? maxKey, bool exclusive,
        CancellationToken cancellationToken)
    {
        var results = new List<(byte[] Key, byte[] Value)>();
        bool reachedEnd = false;
        uint nextLeaf = 0;
        
        var page = await m_pageManager.GetPageAsync(pageNumber, cancellationToken).ConfigureAwait(false);
        try
        {
            var node = new BTreeNode(page.Data, PageSize, pageNumber);
            nextLeaf = node.NextLeaf;
            int keyCount = node.KeyCount;
            
            for (int i = startIndex; i < keyCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var keyBytes = node.GetKey(i).ToArray();
                
                // Check end boundary
                if (maxKey != null)
                {
                    int cmp = keyBytes.AsSpan().SequenceCompareTo(maxKey);
                    if (exclusive ? cmp >= 0 : cmp > 0)
                    {
                        reachedEnd = true;
                        break;
                    }
                }
                
                byte[] valueBytes;
                if (node.IsOverflowValue(i))
                {
                    uint overflowPage = node.GetOverflowPage(i);
                    m_pageManager.ReleasePage(pageNumber);
                    page = null!;
                    
                    valueBytes = await m_pageManagerOverflowManager.ReadOverflowAsync(overflowPage, cancellationToken).ConfigureAwait(false);
                    
                    page = await m_pageManager.GetPageAsync(pageNumber, cancellationToken).ConfigureAwait(false);
                    node = new BTreeNode(page.Data, PageSize, pageNumber);
                }
                else
                {
                    valueBytes = node.GetValue(i).ToArray();
                }
                
                results.Add((keyBytes, valueBytes));
            }
            
            return (results, nextLeaf, reachedEnd);
        }
        finally
        {
            if (page != null!)
            {
                m_pageManager.ReleasePage(pageNumber);
            }
        }
    }

    #endregion
}
