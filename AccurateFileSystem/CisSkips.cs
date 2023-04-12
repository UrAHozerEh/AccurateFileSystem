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
        public List<double> Footages { get; set; }

        public Skips(string name, List<double> footages) : base(name, FileType.Skips)
        {
            Footages = footages;
        }

        public static async Task<Skips> GetSkips(StorageFile file)
        {
            var footages = new List<double>();
            var lines = await file.GetLines();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var val = double.Parse(line);
                footages.Add(val);
            }
            return new Skips(file.DisplayName, footages);
        }

        public override bool IsEquivalent(File otherFile)
        {
            throw new NotImplementedException();
        }
    }
}
