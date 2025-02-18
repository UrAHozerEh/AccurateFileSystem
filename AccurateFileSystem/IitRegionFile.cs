using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Storage;

namespace AccurateFileSystem
{
    public class IitRegionFile : GeneralCsv
    {
        public List<Hca> Hcas { get; private set; }
        private int HcaColumn = -1;
        private int RouteColumn = -1;
        private int BeginMpColumn = -1;
        private int EndMpColumn = -1;
        private int BeginGpsColumn = -1;
        private int EndGpsColumn = -1;
        private int RegionColumn = -1;
        private int FirstTimeColumn = -1;
        private int ThirdToolColumn = -1;

        public IitRegionFile(string name, List<string> lines) : base(name, lines)
        {
            Hcas = new List<Hca>();
            GetColumns();
            ParseData();
        }

        public Hca GetHca(string name)
        {
            foreach (var hca in Hcas)
            {
                if (hca.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return hca;
                }
            }
            return null;
        }

        private void GetColumns()
        {
            for (var i = 0; i < Headers.Count; ++i)
            {
                var header = Headers[i];
                if (header.Contains("region", StringComparison.OrdinalIgnoreCase))
                    RegionColumn = i;
                if (header.Contains("hca", StringComparison.OrdinalIgnoreCase))
                    HcaColumn = i;
                if (header.Contains("route", StringComparison.OrdinalIgnoreCase))
                    RouteColumn = i;
                if (header.Contains("1st time", StringComparison.OrdinalIgnoreCase))
                    FirstTimeColumn = i;
                if (header.Contains("3rd iit", StringComparison.OrdinalIgnoreCase))
                    ThirdToolColumn = i;
                if (header.Contains("begin", StringComparison.OrdinalIgnoreCase))
                {
                    if (header.Contains("mp", StringComparison.OrdinalIgnoreCase))
                        BeginMpColumn = i;
                    if (header.Contains("lat", StringComparison.OrdinalIgnoreCase))
                        BeginGpsColumn = i;
                }
                if (header.Contains("end", StringComparison.OrdinalIgnoreCase))
                {
                    if (header.Contains("mp", StringComparison.OrdinalIgnoreCase))
                        EndMpColumn = i;
                    if (header.Contains("lat", StringComparison.OrdinalIgnoreCase))
                        EndGpsColumn = i;
                }
            }
        }

        private (string Latitude, string Longitude) SplitGps(string gps)
        {
            gps = gps.Replace(',', ' ');
            gps = gps.Replace(';', ' ').Trim();
            gps = gps.Replace('/', ' ').Trim();
            gps = gps.Replace('\\', ' ').Trim();
            var lat = "";
            var lon = "";
            var split = gps.Split(' ');
            lat = split[0];
            for (var i = 1; i < split.Length; ++i)
            {
                if (!string.IsNullOrWhiteSpace(split[i]))
                {
                    lon = split[i];
                    break;
                }
            }
            return (lat.Trim(), lon.Trim());
        }

        private void ParseData()
        {
            var startName = "";
            var startLineName = "";
            List<string[]> curLines = null;
            bool? isFirstTime = null;
            for (var i = 0; i < Data.GetLength(0); ++i)
            {
                var curName = Data[i, HcaColumn].Trim();

                var route = Data[i, RouteColumn].Trim();

                var beginMp = Data[i, BeginMpColumn].Trim();
                var endMp = Data[i, EndMpColumn].Trim();

                var beginGps = SplitGps(Data[i, BeginGpsColumn]);
                var endGps = SplitGps(Data[i, EndGpsColumn]);

                var region = Data[i, RegionColumn].Trim();
                region = region.Replace('–', '-');
                //if (region.Contains('-'))
                //    region = region.Substring(0, region.IndexOf('-')).Trim();
                

                if (ThirdToolColumn != -1)
                {
                    var thirdToolValue = Data[i, ThirdToolColumn].Trim();
                    if (thirdToolValue.Contains("pcm", StringComparison.OrdinalIgnoreCase))
                    {
                        region += "P";
                    }
                }

                var line = new string[] { curName, route, beginMp, endMp, beginGps.Latitude, beginGps.Longitude, endGps.Latitude, endGps.Longitude, region, Data[i, FirstTimeColumn] };
                if (startName == "")
                {
                    startName = curName;
                    startLineName = Data[i, RouteColumn].Trim();
                    curLines = new List<string[]>() { line };
                    continue;
                }
                if (curName == startName)
                {
                    curLines.Add(line);
                    continue;
                }
                Hcas.Add(new Hca(startName, startLineName, curLines));
                startName = curName;
                startLineName = line[1].Trim();
                curLines = new List<string[]>() { line };
            }
            Hcas.Add(new Hca(startName, startLineName, curLines));
            Debug.WriteLine(string.Join("\n", Hcas));
        }

        public static async Task<IitRegionFile> GetIitRegion(StorageFile file)
        {
            var factory = new FileFactory(file);
            var newFile = await factory.GetFile();
            if (!(newFile is GeneralCsv))
            {
                throw new Exception();
            }
            var lines = await file.GetLines();
            return new IitRegionFile(file.DisplayName, lines);
        }

        public override bool IsEquivalent(File otherFile)
        {
            throw new NotImplementedException();
        }
    }
}
