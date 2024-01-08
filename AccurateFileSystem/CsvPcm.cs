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
        public List<(BasicGeoposition Gps, double Depth, string Date)> DepthData { get; set; } = new List<(BasicGeoposition Gps, double Depth, string Date)>();
        public List<(BasicGeoposition Gps, double Amps, string Date)> AmpData { get; set; } = new List<(BasicGeoposition Gps, double Amps, string Date)>();
        public List<(BasicGeoposition Gps, string Date)> TxData { get; set; } = new List<(BasicGeoposition Gps, string Date)>();

        protected CsvPcm(string name, List<string> lines) : base(name, lines, FileType.PCM)
        {

        }

        protected void GetTxData(int latColumn, int lonColumn, int txColumn, int dateColumn)
        {
            TxData = new List<(BasicGeoposition Gps, string Date)>();
            if (Data.GetLength(0) == 0)
                Data = Data;
            for (var r = 0; r < Data.GetLength(0); ++r)
            {
                var lat = GetDecimalDegree(Data[r, latColumn]);
                var lon = GetDecimalDegree(Data[r, lonColumn]);
                var gps = new BasicGeoposition() { Latitude = lat, Longitude = lon };
                if (string.IsNullOrWhiteSpace(Data[r, txColumn]))
                    continue;
                TxData.Add((gps, Data[r, dateColumn]));
            }
        }

        protected void GetDepthData(int latColumn, int lonColumn, int depthColumn, int dateColumn)
        {
            DepthData = new List<(BasicGeoposition Gps, double Depth, string Date)>();
            if (Data.GetLength(0) == 0)
                Data = Data;
            for (var r = 0; r < Data.GetLength(0); ++r)
            {
                var lat = GetDecimalDegree(Data[r, latColumn]);
                var lon = GetDecimalDegree(Data[r, lonColumn]);
                var gps = new BasicGeoposition() { Latitude = lat, Longitude = lon };
                var depthString = Data[r, depthColumn];
                var depth = 0.0;
                if (!string.IsNullOrWhiteSpace(depthString))
                    depth = double.Parse(Data[r, depthColumn]);
                if (depth == 0)
                    continue;
                DepthData.Add((gps, depth, Data[r, dateColumn]));
            }
        }

        protected void GetAmpData(int latColumn, int lonColumn, int ampColumn, int dateColumn)
        {
            AmpData = new List<(BasicGeoposition Gps, double Depth, string Date)>();
            for (var r = 0; r < Data.GetLength(0); ++r)
            {
                var lat = GetDecimalDegree(Data[r, latColumn]);
                var lon = GetDecimalDegree(Data[r, lonColumn]);
                var gps = new BasicGeoposition() { Latitude = lat, Longitude = lon };
                var ampString = Data[r, ampColumn];
                var amps = 0.0;
                if (!string.IsNullOrWhiteSpace(ampString))
                    amps = double.Parse(Data[r, ampColumn]) * 1000;
                if (amps == 0)
                    continue;
                AmpData.Add((gps, amps, Data[r, dateColumn]));
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
