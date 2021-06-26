using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace AccurateFileSystem
{
    public abstract class CsvPcm : GeneralCsv
    {
        public List<(BasicGeoposition Gps, double Depth)> DepthData { get; set; }

        protected CsvPcm(string name, List<string> lines) : base(name, lines, FileType.PCM)
        {

        }

        protected void GetDepthData(int latColumn, int lonColumn, int depthColumn)
        {
            DepthData = new List<(BasicGeoposition Gps, double Depth)>();
            for (int r = 0; r < Data.GetLength(0); ++r)
            {
                var lat = GetDecimalDegree(Data[r, latColumn]);
                var lon = GetDecimalDegree(Data[r, lonColumn]);
                var gps = new BasicGeoposition() { Latitude = lat, Longitude = lon };
                var depthString = Data[r, depthColumn];
                double depth = 0.0;
                if(!string.IsNullOrWhiteSpace(depthString))
                    depth = double.Parse(Data[r, depthColumn]);
                if (depth == 0)
                    continue;
                DepthData.Add((gps, depth));
            }
        }

        private double GetDecimalDegree(string value)
        {
            if (value.Contains('�'))
                return value.ParseDegree();
            return double.Parse(value);
        }
    }
}
