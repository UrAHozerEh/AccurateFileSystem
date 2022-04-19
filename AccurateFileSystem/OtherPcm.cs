using System;
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
                    case "Depth":
                        depthColumn = i;
                        break;
                    case "Depth(in)":
                        depthColumn = i;
                        break;
                    case "Signal_Cur":
                        ampColumn = i;
                        break;
                }
            }
            GetDepthData(latColumn, lonColumn, depthColumn);
            if (ampColumn != -1)
                GetAmpData(latColumn, lonColumn, ampColumn);
        }
    }
}
