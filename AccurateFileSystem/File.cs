using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateFileSystem
{
    public abstract class File : Object
    {
        public FileType Type { get; }

        public File(string name, FileType type) : base(name)
        {
            Type = type;
        }

        public abstract bool IsEquivalent(File otherFile);
    }

    public enum FileType
    {
        OnOff, CISWaveform, RISWaveform, DCVG, ACVG, PCM, SoilRes, Native, Udl, Unknown
    }
}
