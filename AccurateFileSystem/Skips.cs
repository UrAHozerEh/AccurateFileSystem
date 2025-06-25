using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Devices.Geolocation;
using Windows.Storage;

namespace AccurateFileSystem
{
    public class Skips : File
    {
        public List<Skip> Locations { get; set; }

        public Skips(string name, List<Skip> locations) : base(name, FileType.Skips)
        {
            Locations = locations;
        }

        public static async Task<Skips> GetSkips(StorageFile file)
        {
            var locations = new List<Skip>();
            var lines = await file.GetLines();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var split = line.Split(',');
                if (double.TryParse(split[0], out _) && double.TryParse(split[1], out _))
                {
                    var skip = ParseGpsSkip(split);
                    if (skip != null)
                    {
                        locations.Add(skip);
                    }
                }
                else
                {
                    var skip = ParseFootageSkip(split);
                    if (skip != null)
                    {
                        locations.Add(skip);
                    }
                }
            }
            return new Skips(file.DisplayName, locations);
        }

        private static Skip ParseFootageSkip(string[] split)
        {
            if (split.Length < 3) return null;
            var foot = double.Parse(split[0]);
            var name = split[1].Trim();
            var firstTime = split[2].Trim().Contains("y", StringComparison.OrdinalIgnoreCase);
            if (split.Length > 3)
            {
                var shortSkip = split[3].Trim();
                var longSkip = split[4].Trim();
                return new Skip(foot, new HcaRegion(name, firstTime, shortSkip, longSkip));
            }
            return new Skip(foot, new HcaRegion(name, firstTime));
        }

        private static Skip ParseGpsSkip(string[] split)
        {
            if (split.Length < 4) return null;
            var lat = double.Parse(split[0]);
            var lon = double.Parse(split[1]);
            var name = split[2].Trim();
            var firstTime = split[3].Trim().Contains("y", StringComparison.OrdinalIgnoreCase);
            var gps = new BasicGeoposition { Latitude = lat, Longitude = lon };
            if(split.Length > 4)
            {
                var shortSkip = split[4].Trim();
                var longSkip = split[5].Trim();
                return new Skip(gps, new HcaRegion(name, firstTime, shortSkip, longSkip));
            }
            return new Skip(gps, new HcaRegion(name, firstTime));
        }

        public override bool IsEquivalent(File otherFile)
        {
            throw new NotImplementedException();
        }
    }

    public class Skip
    {
        public double Footage { get; }
        public BasicGeoposition Gps { get; }
        public bool HasRegion => Region != null;
        public HcaRegion Region { get; } = null;
        public bool HasFootage { get; }
        public bool HasGps => !HasFootage;

        public Skip(double footage, HcaRegion region)
        {
            Footage = footage;
            Region = region;
            HasFootage = true;
        }

        public Skip(BasicGeoposition gps, HcaRegion region)
        {
            Gps = gps;
            Region = region;
            HasFootage = false;
        }
    }
}
