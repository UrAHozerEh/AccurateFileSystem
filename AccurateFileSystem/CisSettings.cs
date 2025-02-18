using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateFileSystem
{
    public class CisSettings : File
    {
        public bool HasMinOffValue => MinOffValue.HasValue;
        public double? MinOffValue { get; set; } = -0.850;
        public bool HasMaxOffValue => MaxOffValue.HasValue;
        public double? MaxOffValue { get; set; } = null;
        public double CisGap { get; set; } = 20;
        public bool UseMir { get; set; } = false;


        public CisSettings(string name, List<string> lines) : base(name, FileType.CisSettings)
        {
            if (lines[0].StartsWith("CIS Settings", StringComparison.OrdinalIgnoreCase))
                lines.RemoveAt(0);
        }

        public CisSettings() : base("Default Settings", FileType.CisSettings)
        {

        }

        public override bool IsEquivalent(File otherFile)
        {
            throw new NotImplementedException();
        }
    }
}
