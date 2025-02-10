using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace AccurateFileSystem
{
    public class GlobalGpsShift : File
    {
        public double ReportDLatitudeShift { get; set; } = double.NaN;
        public double ReportDLongitudeShift { get; set; } = double.NaN;
        public bool HasReportD => !double.IsNaN(ReportDLatitudeShift) && !double.IsNaN(ReportDLongitudeShift);
        public double PreLatitudeShift { get; set; } = double.NaN;
        public double PreLongitudeShift { get; set; } = double.NaN;
        public bool HasPre => !double.IsNaN(PreLatitudeShift) && !double.IsNaN(PreLongitudeShift);
        public double PostLatitudeShift { get; set; } = double.NaN;
        public double PostLongitudeShift { get; set; } = double.NaN;
        public bool HasPost => !double.IsNaN(PostLatitudeShift) && !double.IsNaN(PostLongitudeShift);

        public GlobalGpsShift(string name) : base(name, FileType.GlobalGpsShift)
        {

        }

        public static async Task<GlobalGpsShift> GetGpsShift(StorageFile file)
        {
            var output = new GlobalGpsShift(file.DisplayName);
            var lines = await file.GetLines();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var split = line.Split(',');
                var shiftType = "";
                var shiftLat = 0.0;
                var shiftLong = 0.0;
                if (split.Length == 3)
                {
                    shiftType = split[0].Trim().ToLower();
                    shiftLat = double.Parse(split[1].Trim());
                    shiftLong = double.Parse(split[2].Trim());
                }
                if(split.Length == 5)
                {
                    shiftType = split[0].Trim().ToLower();
                    var startLat = double.Parse(split[1].Trim());
                    var startLon = double.Parse(split[2].Trim());
                    var endLat = double.Parse(split[3].Trim());
                    var endLon = double.Parse(split[4].Trim());
                    shiftLat = endLat - startLat;
                    shiftLong = endLon - startLon;
                }
                if (shiftType == "d")
                {
                    output.ReportDLatitudeShift = shiftLat;
                    output.ReportDLongitudeShift = shiftLong;
                }
                else if (shiftType == "pre")
                {
                    output.PreLatitudeShift = shiftLat;
                    output.PreLongitudeShift = shiftLong;
                }
                else if (shiftType == "post")
                {
                    output.PostLatitudeShift = shiftLat;
                    output.PostLongitudeShift = shiftLong;
                }
            }
            return output;
        }


        public override bool IsEquivalent(File otherFile)
        {
            throw new NotImplementedException();
        }
    }
}
