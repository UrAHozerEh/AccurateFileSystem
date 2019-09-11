using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateFileSystem
{
    public abstract class File : Object
    {
        public abstract FileType Type { get; }
    }

    public enum FileType
    {
        OnOff, CISWaveform, RISWaveform, DCVG, ACVG, PCM, SoilRes, Native, Unknown
    }
}
