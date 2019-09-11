using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateFileSystem
{
    public class AllegroWaveformFile : File
    {
        public override FileType Type => FileType.CISWaveform;

        public struct DataPoint
        {
            public double Value;
            public bool IsOn;
        }
    }
}
