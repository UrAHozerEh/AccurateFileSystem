﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateFileSystem
{
    public class OtherPcm : CsvPcm
    {
        public OtherPcm(string name, List<string> lines) : base(name, lines)
        {
            ParseData();
        }

        private void ParseData()
        {
            var latColumn = -1;
            var lonColumn = -1;
            var depthColumn = -1;
            var ampColumn = -1;
            var dateColumn = -1;
            var txColumn = -1;
            for (var i = 0; i < Headers.Count; ++i)
            {
                var header = Headers[i];
                switch (header)
                {
                    case "Latitude":
                        latColumn = i;
                        break;
                    case "Longitude":
                        lonColumn = i;
                        break;
                    case "Depth":
                        depthColumn = i;
                        break;
                    case "Depth(in)":
                        depthColumn = i;
                        break;
                    case "Signal_Cur":
                        ampColumn = i;
                        break;
                    case "GPS_Date":
                        dateColumn = i;
                        break;
                    case "Transmitte":
                        txColumn = i;
                        break;
                }
            }
            if(depthColumn != -1)
                GetDepthData(latColumn, lonColumn, depthColumn, dateColumn);
            if (ampColumn != -1)
                GetAmpData(latColumn, lonColumn, ampColumn, dateColumn);
            if (txColumn != -1)
                GetTxData(latColumn, lonColumn, txColumn, dateColumn);
        }
    }
}
