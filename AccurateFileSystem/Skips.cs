using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace AccurateFileSystem
{
    public class Skips : File
    {
        public List<Skip> Footages { get; set; }

        public Skips(string name, List<Skip> footages) : base(name, FileType.Skips)
        {
            Footages = footages;
        }

        public static async Task<Skips> GetSkips(StorageFile file)
        {
            var footages = new List<Skip>();
            var lines = await file.GetLines();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var split = line.Split(',');
                var footage = double.Parse(split[0].Trim());
                var name = "";
                bool? firstTime = null;
                if (split.Length > 1)
                {
                    name = split[1].Trim();
                }
                if (split.Length > 2)
                {
                    firstTime = split[2].Trim().Contains("y", StringComparison.OrdinalIgnoreCase);
                }

                if(split.Length > 1)
                {
                    footages.Add(new Skip(footage, new HcaRegion(name, firstTime)));
                }
                else
                {
                    footages.Add(new Skip(footage));
                }
            }
            return new Skips(file.DisplayName, footages);
        }

        public override bool IsEquivalent(File otherFile)
        {
            throw new NotImplementedException();
        }
    }

    public class Skip
    {
        public double Footage { get; }
        public bool HasRegion => Region != null;
        public HcaRegion Region { get; } = null;

        public Skip(double footage, HcaRegion region = null)
        {
            Footage = footage;
            Region = region;
        }
    }
}
