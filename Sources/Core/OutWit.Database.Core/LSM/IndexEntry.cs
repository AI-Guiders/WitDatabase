using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OutWit.Database.Core.LSM
{
    internal struct IndexEntry
    {
        public byte[] FirstKey;
        public long BlockOffset;
        public int BlockSize;
    }
}
