using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace AccurateFileSystem
{
    public class VivaxPcm : CsvPcm
    {
        public VivaxPcm(string name, List<string> lines) : base(name, lines.Where(line => !line.StartsWith("Observations")).ToList())
        {
            ParseData();
        }

        private void ParseData()
        {
            var latColumn = -1;
            var lonColumn = -1;
            var depthColumn = -1;
            for (int i = 0; i < Headers.Count; ++i)
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
                    case "Depth(in)":
                        depthColumn = i;
                        break;
                }
            }
            GetDepthData(latColumn, lonColumn, depthColumn);
        }
    }
}
