using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateFileSystem
{
    public class AllegroCISFile : File
    {
        public Dictionary<string, string> Header { get; private set; }
        public override FileType Type => type;
        private FileType type;
        public AllegroCISFile(Dictionary<string, string> header, Dictionary<int, AllegroDataPoint> data, FileType type)
        {
            this.type = type;
        }
    }
}
