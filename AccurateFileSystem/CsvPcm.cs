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
        public List<(BasicGeoposition Gps, string Date, double dB)> AcvgData { get; set; } = new List<(BasicGeoposition Gps, string Date, double dB)>();

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
                var date = "";
                if (dateColumn != -1)
                {
                    date = Data[r, dateColumn];
                }
                TxData.Add((gps, date));
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
                var date = "";
                if (dateColumn != -1)
                {
                    date = Data[r, dateColumn];
                }
                DepthData.Add((gps, depth, date));
            }
        }

        protected void GetAcvgData(int latColumn, int lonColumn, int dbColumn, int dateColumn)
        {
            AcvgData = new List<(BasicGeoposition Gps, string Date, double Depth)>();
            if (Data.GetLength(0) == 0)
                Data = Data;
            for (var r = 0; r < Data.GetLength(0); ++r)
            {
                var lat = GetDecimalDegree(Data[r, latColumn]);
                var lon = GetDecimalDegree(Data[r, lonColumn]);
                var gps = new BasicGeoposition() { Latitude = lat, Longitude = lon };
                var dbString = Data[r, dbColumn];
                var dB = 0.0;
                if (!string.IsNullOrWhiteSpace(dbString))
                    dB = double.Parse(dbString);
                if (dB == 0)
                    continue;
                var date = "";
                if (dateColumn != -1)
                {
                    date = Data[r, dateColumn];
                }
                AcvgData.Add((gps, date, dB));
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
                var date = "";
                if (dateColumn != -1)
                {
                    date = Data[r, dateColumn];
                }
                AmpData.Add((gps, amps, date));
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
