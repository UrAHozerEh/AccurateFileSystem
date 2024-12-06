using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace AccurateFileSystem
{
    public class Spacers : File
    {
        public List<Spacer> Data { get; set; }

        public Spacers(string name, List<Spacer> data) : base(name, FileType.Spacers)
        {
            Data = data;
        }

        public static async Task<Spacers> GetSpacers(StorageFile file)
        {
            var footages = new List<Spacer>();
            var lines = await file.GetLines();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(',');
                if (parts.Length != 2) continue;
                var footage = double.Parse(parts[0]);
                var length = double.Parse(parts[1]);
                if(length <= 0) continue;
                footages.Add(new Spacer(footage, length));
            }
            return new Spacers(file.DisplayName, footages);
        }

        public override bool IsEquivalent(File otherFile)
        {
            throw new NotImplementedException();
        }

        public struct Spacer
        {
            public double Footage { get; }
            public double Length { get; }

            public Spacer(double footage, double length)
            {
                Footage = footage;
                Length = length;
            }

            public void Deconstruct(out double footage, out double length)
            {
                footage = Footage;
                length = Length;
            }
        }
    }
}
