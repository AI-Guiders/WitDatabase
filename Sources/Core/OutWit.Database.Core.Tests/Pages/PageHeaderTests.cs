using OutWit.Database.Core.Pages;

namespace OutWit.Database.Core.Tests.Pages
{
    [TestFixture]
    public class PageHeaderTest
    {
        [Test]
        public void CreateEmptyLeafPageTest()
        {
            var header = PageHeader.CreateEmpty(PageType.Leaf, 4096);
        
            Assert.That(header.PageType, Is.EqualTo(PageType.Leaf));
            Assert.That(header.Flags, Is.EqualTo((byte)0));
            Assert.That(header.CellCount, Is.EqualTo((ushort)0));
            Assert.That(header.FreeSpaceStart, Is.EqualTo((ushort)4096));
            Assert.That(header.FragmentedFreeSpace, Is.EqualTo((ushort)0));
            Assert.That(header.RightChild, Is.EqualTo(0u));
        }

        [Test]
        public void WriteReadRoundtripTest()
        {
            var original = new PageHeader
            {
                PageType = PageType.Internal,
                Flags = 0x42,
                CellCount = 100,
                FreeSpaceStart = 2048,
                FragmentedFreeSpace = 256,
                RightChild = 12345,
                Reserved = 0
            };

            byte[] buffer = new byte[PageHeader.PAGE_HEADER_SIZE];
            original.WriteTo(buffer);
        
            var restored = PageHeader.ReadFrom(buffer);
        
            Assert.That(restored.PageType, Is.EqualTo(original.PageType));
            Assert.That(restored.Flags, Is.EqualTo(original.Flags));
            Assert.That(restored.CellCount, Is.EqualTo(original.CellCount));
            Assert.That(restored.FreeSpaceStart, Is.EqualTo(original.FreeSpaceStart));
            Assert.That(restored.FragmentedFreeSpace, Is.EqualTo(original.FragmentedFreeSpace));
            Assert.That(restored.RightChild, Is.EqualTo(original.RightChild));
        }

        [Test]
        public void AllPageTypesRoundtripTest()
        {
            byte[] buffer = new byte[PageHeader.PAGE_HEADER_SIZE];
        
            foreach (PageType pageType in Enum.GetValues<PageType>())
            {
                var original = PageHeader.CreateEmpty(pageType, 4096);
                original.WriteTo(buffer);
                var restored = PageHeader.ReadFrom(buffer);
            
                Assert.That(restored.PageType, Is.EqualTo(pageType));
            }
        }

        [Test]
        public void BufferTooSmallThrowsTest()
        {
            var header = PageHeader.CreateEmpty(PageType.Leaf, 4096);
            byte[] smallBuffer = new byte[8];
        
            Assert.Throws<ArgumentException>(() => header.WriteTo(smallBuffer));
        }

        [Test]
        public void ReadFromTooSmallBufferThrowsTest()
        {
            byte[] smallBuffer = new byte[8];
        
            Assert.Throws<ArgumentException>(() => PageHeader.ReadFrom(smallBuffer));
        }

        [Test]
        public void HeaderSizeIsCorrectTest()
        {
            Assert.That(PageHeader.PAGE_HEADER_SIZE, Is.EqualTo(16));
        }
    }
}
