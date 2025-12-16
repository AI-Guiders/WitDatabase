using OutWit.Database.Core.Encoding;
using OutWit.Database.Core.Pages;

namespace OutWit.Database.Core.Tests.Pages;

[TestFixture]
public class PageTest
{
    private const int PAGE_SIZE = 4096;

    #region Helper Methods

    /// <summary>
    /// Creates a cell with varint-encoded size prefix followed by payload.
    /// </summary>
    private static byte[] CreateCell(byte[] payload)
    {
        int varintLength = VarInt.GetEncodedLengthUnsigned((ulong)payload.Length);
        byte[] cell = new byte[varintLength + payload.Length];
        VarInt.EncodeUnsigned(cell, (ulong)payload.Length);
        payload.CopyTo(cell, varintLength);
        return cell;
    }

    /// <summary>
    /// Creates a simple cell with specified data byte repeated.
    /// </summary>
    private static byte[] CreateSimpleCell(byte dataByte, int payloadSize)
    {
        byte[] payload = new byte[payloadSize];
        Array.Fill(payload, dataByte);
        return CreateCell(payload);
    }

    #endregion

    #region Initialize Tests

    [Test]
    public void InitializeLeafPageTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);

        page.Initialize(PageType.Leaf);

        Assert.That(page.Header.PageType, Is.EqualTo(PageType.Leaf));
        Assert.That(page.Header.CellCount, Is.EqualTo(0));
        Assert.That(page.Header.FreeSpaceStart, Is.EqualTo(PAGE_SIZE));
        Assert.That(page.Header.FragmentedFreeSpace, Is.EqualTo(0));
    }

    [Test]
    public void InitializeInternalPageTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);

        page.Initialize(PageType.Internal);

        Assert.That(page.Header.PageType, Is.EqualTo(PageType.Internal));
        Assert.That(page.Header.CellCount, Is.EqualTo(0));
    }

    [Test]
    public void InitializeClearsExistingDataTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        Array.Fill(buffer, (byte)0xFF);

        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        // Data after header should be cleared
        Assert.That(buffer[PageHeader.PAGE_HEDER_SIZE], Is.EqualTo(0));
        Assert.That(buffer[PAGE_SIZE - 1], Is.EqualTo(0));
    }

    #endregion

    #region InsertCell Tests

    [Test]
    public void InsertSingleCellTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell = CreateSimpleCell(0xAA, 10);
        int result = page.InsertCell(0, cell);

        Assert.That(result, Is.EqualTo(0));
        Assert.That(page.Header.CellCount, Is.EqualTo(1));
    }

    [Test]
    public void InsertMultipleCellsTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell1 = CreateSimpleCell(0xAA, 10);
        byte[] cell2 = CreateSimpleCell(0xBB, 20);
        byte[] cell3 = CreateSimpleCell(0xCC, 15);

        page.InsertCell(0, cell1);
        page.InsertCell(1, cell2);
        page.InsertCell(2, cell3);

        Assert.That(page.Header.CellCount, Is.EqualTo(3));
    }

    [Test]
    public void InsertCellAtBeginningTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell1 = CreateSimpleCell(0xAA, 10);
        byte[] cell2 = CreateSimpleCell(0xBB, 10);

        page.InsertCell(0, cell1);
        page.InsertCell(0, cell2); // Insert at beginning

        Assert.That(page.Header.CellCount, Is.EqualTo(2));

        // cell2 should be at index 0, cell1 at index 1
        var retrievedCell0 = page.GetCell(0);
        var retrievedCell1 = page.GetCell(1);

        Assert.That(retrievedCell0.ToArray(), Is.EqualTo(cell2));
        Assert.That(retrievedCell1.ToArray(), Is.EqualTo(cell1));
    }

    [Test]
    public void InsertCellInMiddleTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell1 = CreateSimpleCell(0xAA, 10);
        byte[] cell2 = CreateSimpleCell(0xBB, 10);
        byte[] cell3 = CreateSimpleCell(0xCC, 10);

        page.InsertCell(0, cell1);
        page.InsertCell(1, cell2);
        page.InsertCell(1, cell3); // Insert in middle

        Assert.That(page.Header.CellCount, Is.EqualTo(3));

        Assert.That(page.GetCell(0).ToArray(), Is.EqualTo(cell1));
        Assert.That(page.GetCell(1).ToArray(), Is.EqualTo(cell3));
        Assert.That(page.GetCell(2).ToArray(), Is.EqualTo(cell2));
    }

    [Test]
    public void InsertCellUpdatesHeaderTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        ushort initialFreeSpaceStart = page.Header.FreeSpaceStart;
        byte[] cell = CreateSimpleCell(0xAA, 100);

        page.InsertCell(0, cell);

        Assert.That(page.Header.FreeSpaceStart, Is.EqualTo(initialFreeSpaceStart - cell.Length));
    }

    [Test]
    public void InsertCellNegativeIndexThrowsTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell = CreateSimpleCell(0xAA, 10);

        try
        {
            page.InsertCell(-1, cell);
            Assert.Fail("Expected ArgumentOutOfRangeException");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Test]
    public void InsertCellInvalidIndexThrowsTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell = CreateSimpleCell(0xAA, 10);

        // Can't insert at index 1 when there are 0 cells
        try
        {
            page.InsertCell(1, cell);
            Assert.Fail("Expected ArgumentOutOfRangeException");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Test]
    public void InsertCellNotEnoughSpaceReturnsMinusOneTest()
    {
        byte[] buffer = new byte[256]; // Small page
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        // Try to insert a cell that's too large
        byte[] largeCell = CreateSimpleCell(0xAA, 300);
        int result = page.InsertCell(0, largeCell);

        Assert.That(result, Is.EqualTo(-1));
        Assert.That(page.Header.CellCount, Is.EqualTo(0));
    }

    #endregion

    #region GetCell Tests

    [Test]
    public void GetCellReturnsCorrectDataTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell = CreateSimpleCell(0xAA, 50);
        page.InsertCell(0, cell);

        var retrieved = page.GetCell(0);

        Assert.That(retrieved.ToArray(), Is.EqualTo(cell));
    }

    [Test]
    public void GetCellMultipleCellsTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell1 = CreateSimpleCell(0xAA, 10);
        byte[] cell2 = CreateSimpleCell(0xBB, 20);
        byte[] cell3 = CreateSimpleCell(0xCC, 30);

        page.InsertCell(0, cell1);
        page.InsertCell(1, cell2);
        page.InsertCell(2, cell3);

        Assert.That(page.GetCell(0).ToArray(), Is.EqualTo(cell1));
        Assert.That(page.GetCell(1).ToArray(), Is.EqualTo(cell2));
        Assert.That(page.GetCell(2).ToArray(), Is.EqualTo(cell3));
    }

    [Test]
    public void GetCellNegativeIndexThrowsTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell = CreateSimpleCell(0xAA, 10);
        page.InsertCell(0, cell);

        try
        {
            _ = page.GetCell(-1);
            Assert.Fail("Expected ArgumentOutOfRangeException");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Test]
    public void GetCellInvalidIndexThrowsTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell = CreateSimpleCell(0xAA, 10);
        page.InsertCell(0, cell);

        try
        {
            _ = page.GetCell(1);
            Assert.Fail("Expected ArgumentOutOfRangeException");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    #endregion

    #region GetCellPointer Tests

    [Test]
    public void GetCellPointerTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell = CreateSimpleCell(0xAA, 10);
        page.InsertCell(0, cell);

        ushort pointer = page.GetCellPointer(0);

        // Pointer should be at the end of the page minus cell size
        Assert.That(pointer, Is.EqualTo(PAGE_SIZE - cell.Length));
    }

    [Test]
    public void GetCellPointerNegativeIndexThrowsTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell = CreateSimpleCell(0xAA, 10);
        page.InsertCell(0, cell);

        try
        {
            _ = page.GetCellPointer(-1);
            Assert.Fail("Expected ArgumentOutOfRangeException");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Test]
    public void GetCellPointerInvalidIndexThrowsTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        try
        {
            _ = page.GetCellPointer(0);
            Assert.Fail("Expected ArgumentOutOfRangeException");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    #endregion

    #region DeleteCell Tests

    [Test]
    public void DeleteSingleCellTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell = CreateSimpleCell(0xAA, 10);
        page.InsertCell(0, cell);
        page.DeleteCell(0);

        Assert.That(page.Header.CellCount, Is.EqualTo(0));
    }

    [Test]
    public void DeleteCellUpdatesFragmentedSpaceTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell = CreateSimpleCell(0xAA, 10);
        page.InsertCell(0, cell);

        ushort fragmentedBefore = page.Header.FragmentedFreeSpace;
        page.DeleteCell(0);

        Assert.That(page.Header.FragmentedFreeSpace, Is.GreaterThan(fragmentedBefore));
    }

    [Test]
    public void DeleteFirstCellShiftsPointersTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell1 = CreateSimpleCell(0xAA, 10);
        byte[] cell2 = CreateSimpleCell(0xBB, 10);
        byte[] cell3 = CreateSimpleCell(0xCC, 10);

        page.InsertCell(0, cell1);
        page.InsertCell(1, cell2);
        page.InsertCell(2, cell3);

        page.DeleteCell(0);

        Assert.That(page.Header.CellCount, Is.EqualTo(2));
        Assert.That(page.GetCell(0).ToArray(), Is.EqualTo(cell2));
        Assert.That(page.GetCell(1).ToArray(), Is.EqualTo(cell3));
    }

    [Test]
    public void DeleteMiddleCellShiftsPointersTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell1 = CreateSimpleCell(0xAA, 10);
        byte[] cell2 = CreateSimpleCell(0xBB, 10);
        byte[] cell3 = CreateSimpleCell(0xCC, 10);

        page.InsertCell(0, cell1);
        page.InsertCell(1, cell2);
        page.InsertCell(2, cell3);

        page.DeleteCell(1);

        Assert.That(page.Header.CellCount, Is.EqualTo(2));
        Assert.That(page.GetCell(0).ToArray(), Is.EqualTo(cell1));
        Assert.That(page.GetCell(1).ToArray(), Is.EqualTo(cell3));
    }

    [Test]
    public void DeleteLastCellTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell1 = CreateSimpleCell(0xAA, 10);
        byte[] cell2 = CreateSimpleCell(0xBB, 10);

        page.InsertCell(0, cell1);
        page.InsertCell(1, cell2);

        page.DeleteCell(1);

        Assert.That(page.Header.CellCount, Is.EqualTo(1));
        Assert.That(page.GetCell(0).ToArray(), Is.EqualTo(cell1));
    }

    [Test]
    public void DeleteCellNegativeIndexThrowsTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell = CreateSimpleCell(0xAA, 10);
        page.InsertCell(0, cell);

        try
        {
            page.DeleteCell(-1);
            Assert.Fail("Expected ArgumentOutOfRangeException");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Test]
    public void DeleteCellInvalidIndexThrowsTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell = CreateSimpleCell(0xAA, 10);
        page.InsertCell(0, cell);

        try
        {
            page.DeleteCell(1);
            Assert.Fail("Expected ArgumentOutOfRangeException");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Test]
    public void DeleteCellFromEmptyPageThrowsTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        try
        {
            page.DeleteCell(0);
            Assert.Fail("Expected ArgumentOutOfRangeException");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    #endregion

    #region Defragment Tests

    [Test]
    public void DefragmentEmptyPageTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        page.Defragment();

        Assert.That(page.Header.FreeSpaceStart, Is.EqualTo(PAGE_SIZE));
        Assert.That(page.Header.FragmentedFreeSpace, Is.EqualTo(0));
    }

    [Test]
    public void DefragmentClearsFragmentedSpaceTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        // Insert and delete to create fragmentation
        byte[] cell1 = CreateSimpleCell(0xAA, 50);
        byte[] cell2 = CreateSimpleCell(0xBB, 50);
        byte[] cell3 = CreateSimpleCell(0xCC, 50);

        page.InsertCell(0, cell1);
        page.InsertCell(1, cell2);
        page.InsertCell(2, cell3);

        page.DeleteCell(1); // Creates fragmentation

        Assert.That(page.Header.FragmentedFreeSpace, Is.GreaterThan(0));

        page.Defragment();

        Assert.That(page.Header.FragmentedFreeSpace, Is.EqualTo(0));
    }

    [Test]
    public void DefragmentPreservesCellDataTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell1 = CreateSimpleCell(0xAA, 50);
        byte[] cell2 = CreateSimpleCell(0xBB, 50);
        byte[] cell3 = CreateSimpleCell(0xCC, 50);

        page.InsertCell(0, cell1);
        page.InsertCell(1, cell2);
        page.InsertCell(2, cell3);

        page.DeleteCell(1);
        page.Defragment();

        // Remaining cells should still be correct
        Assert.That(page.GetCell(0).ToArray(), Is.EqualTo(cell1));
        Assert.That(page.GetCell(1).ToArray(), Is.EqualTo(cell3));
    }

    [Test]
    public void DefragmentCompactsCellsTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell1 = CreateSimpleCell(0xAA, 100);
        byte[] cell2 = CreateSimpleCell(0xBB, 100);

        page.InsertCell(0, cell1);
        page.InsertCell(1, cell2);

        page.DeleteCell(0);

        ushort freeSpaceBeforeDefrag = page.Header.FreeSpaceStart;

        page.Defragment();

        // FreeSpaceStart should move up after compaction
        Assert.That(page.Header.FreeSpaceStart, Is.GreaterThan(freeSpaceBeforeDefrag));
    }

    #endregion

    #region Property Tests

    [Test]
    public void UsableSpaceTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        Assert.That(page.UsableSpace, Is.EqualTo(PAGE_SIZE - PageHeader.PAGE_HEDER_SIZE));
    }

    [Test]
    public void CellPointerStartTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        Assert.That(page.CellPointerStart, Is.EqualTo(PageHeader.PAGE_HEDER_SIZE));
    }

    [Test]
    public void CellPointerArraySizeEmptyPageTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        Assert.That(page.CellPointerArraySize, Is.EqualTo(0));
    }

    [Test]
    public void CellPointerArraySizeWithCellsTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell = CreateSimpleCell(0xAA, 10);
        page.InsertCell(0, cell);
        page.InsertCell(1, CreateSimpleCell(0xBB, 10));
        page.InsertCell(2, CreateSimpleCell(0xCC, 10));

        Assert.That(page.CellPointerArraySize, Is.EqualTo(6)); // 3 cells * 2 bytes
    }

    [Test]
    public void FreeSpaceEmptyPageTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        int expectedFreeSpace = PAGE_SIZE - PageHeader.PAGE_HEDER_SIZE;
        Assert.That(page.FreeSpace, Is.EqualTo(expectedFreeSpace));
    }

    [Test]
    public void FreeSpaceAfterInsertTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        int initialFreeSpace = page.FreeSpace;
        byte[] cell = CreateSimpleCell(0xAA, 100);

        page.InsertCell(0, cell);

        // Free space should decrease by cell size + pointer size (2 bytes)
        Assert.That(page.FreeSpace, Is.EqualTo(initialFreeSpace - cell.Length - 2));
    }

    [Test]
    public void DataPropertyReturnsBufferTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        Assert.That(page.Data.Length, Is.EqualTo(PAGE_SIZE));
    }

    #endregion

    #region Integration Tests

    [Test]
    public void InsertDeleteInsertSequenceTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[] cell1 = CreateSimpleCell(0xAA, 50);
        byte[] cell2 = CreateSimpleCell(0xBB, 50);
        byte[] cell3 = CreateSimpleCell(0xCC, 50);
        byte[] cell4 = CreateSimpleCell(0xDD, 50);

        page.InsertCell(0, cell1);
        page.InsertCell(1, cell2);
        page.DeleteCell(0);
        page.InsertCell(0, cell3);
        page.InsertCell(2, cell4);

        Assert.That(page.Header.CellCount, Is.EqualTo(3));
        Assert.That(page.GetCell(0).ToArray(), Is.EqualTo(cell3));
        Assert.That(page.GetCell(1).ToArray(), Is.EqualTo(cell2));
        Assert.That(page.GetCell(2).ToArray(), Is.EqualTo(cell4));
    }

    [Test]
    public void AutoDefragmentOnInsertTest()
    {
        byte[] buffer = new byte[512]; // Small page to force defragmentation
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        // Fill page with cells
        byte[] smallCell = CreateSimpleCell(0xAA, 30);
        for (int i = 0; i < 10; i++)
        {
            int result = page.InsertCell(i, smallCell);
            if (result == -1) break;
        }

        int cellCountBefore = page.Header.CellCount;

        // Delete some cells to create fragmentation
        if (cellCountBefore > 2)
        {
            page.DeleteCell(1);
            page.DeleteCell(1);
        }

        // Insert should trigger defragmentation if needed
        byte[] newCell = CreateSimpleCell(0xBB, 30);
        int insertResult = page.InsertCell(0, newCell);

        // Should either succeed or fail cleanly
        Assert.That(insertResult, Is.EqualTo(0).Or.EqualTo(-1));
    }

    [Test]
    public void LargePayloadCellTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        // Create a cell with payload > 127 bytes to test varint encoding
        byte[] cell = CreateSimpleCell(0xAA, 200);

        page.InsertCell(0, cell);

        var retrieved = page.GetCell(0);
        Assert.That(retrieved.ToArray(), Is.EqualTo(cell));
    }

    [Test]
    public void ManyCellsTest()
    {
        byte[] buffer = new byte[PAGE_SIZE];
        var page = new Page(buffer);
        page.Initialize(PageType.Leaf);

        byte[][] cells = new byte[50][];
        int insertedCount = 0;

        for (int i = 0; i < 50; i++)
        {
            cells[i] = CreateSimpleCell((byte)i, 20);
            int result = page.InsertCell(i, cells[i]);
            if (result == -1) break;
            insertedCount++;
        }

        Assert.That(page.Header.CellCount, Is.EqualTo(insertedCount));

        // Verify all inserted cells
        for (int i = 0; i < insertedCount; i++)
        {
            Assert.That(page.GetCell(i).ToArray(), Is.EqualTo(cells[i]));
        }
    }

    #endregion
}
