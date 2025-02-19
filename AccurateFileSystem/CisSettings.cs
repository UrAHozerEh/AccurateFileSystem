using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateFileSystem
{
    public class CisSettings : File
    {
        public bool HasMinOffValue => MinOffValue.HasValue;
        public double? MinOffValue { get; set; } = -0.850;
        public bool HasMaxOffValue => MaxOffValue.HasValue;
        public double? MaxOffValue { get; set; } = null;
        public double MinDepolValue { get; set; } = -0.1;
        public double DepthGraphMaxValue { get; set; } = 180;
        public double CisGraphMinValue { get; set; } = -3;
        public int CisGraphMajorGridCount { get; set; } = 6;
        public double CisGraphMaxValue { get; set; } = 0;
        public double CisGraphHeight => Math.Abs(CisGraphMinValue - CisGraphMaxValue);
        public double CisGraphMajorGridStep => CisGraphHeight / CisGraphMajorGridCount;
        public double CisGraphMinorGridStep => CisGraphMajorGridStep / 5;
        public double? FeetPerPage { get; set; } = 1000;
        public bool HasFeetPerPage => FeetPerPage.HasValue;
        public double FeetOverlap { get; set; } = 100;
        public double CisGap { get; set; } = 20;
        public bool UseMir { get; set; } = false;
        public bool InvertGraph { get; set; } = true;
        public bool SetFootageFromGps { get; set; } = false;
        public bool StraightenGps => StraightenGpsCommentsDistance.HasValue;
        public double? StraightenGpsCommentsDistance { get; set; } = null;


        public CisSettings(string name, List<string> lines) : base(name, FileType.CisSettings)
        {
            if (lines[0].StartsWith("CIS Settings", StringComparison.OrdinalIgnoreCase))
                lines.RemoveAt(0);
            foreach (var line in lines)
            {
                ParseLine(line);
            }
        }

        private void ParseLine(string line)
        {
            var split = line.Split(',');
            var curLabel = split[0].ToLower().Remove(' ');
            var hasDouble = false;
            var hasBool = false;
            var isBlank = string.IsNullOrWhiteSpace(split[1]);
            var doubleValue = 0.0;
            var boolValue = false;
            if (!isBlank && double.TryParse(split[1], out doubleValue))
                hasDouble = true;
            if (!isBlank && bool.TryParse(split[1], out boolValue))
                hasBool = true;
            if (hasDouble)
            {
                switch (curLabel)
                {
                    case "minoffvalue":
                        MinOffValue = doubleValue;
                        break;
                    case "maxoffvalue":
                        MaxOffValue = doubleValue;
                        break;
                    case "mindepolvalue":
                        MinDepolValue = doubleValue;
                        break;
                    case "depthgraphmaxvalue":
                        DepthGraphMaxValue = doubleValue;
                        break;
                    case "cisgraphminvalue":
                        CisGraphMinValue = doubleValue;
                        break;
                    case "cisgraphmaxvalue":
                        CisGraphMaxValue = doubleValue;
                        break;
                    case "cisgraphmajorgridcount":
                        CisGraphMajorGridCount = (int)doubleValue;
                        break;
                    case "feetperpage":
                        FeetPerPage = doubleValue;
                        break;
                    case "feetoverlap":
                        FeetOverlap = doubleValue;
                        break;
                    case "cismaxgap":
                        CisGap = doubleValue;
                        break;
                    case "straightengpscommentdistance":
                        StraightenGpsCommentsDistance = doubleValue;
                        break;
                    default:
                        break;
                }
            }
            if (hasBool)
            {
                switch (curLabel)
                {
                    case "usemir":
                        UseMir = boolValue;
                        break;
                    case "invertgraph":
                        InvertGraph = boolValue;
                        break;
                    case "setfootagefromgps":
                        SetFootageFromGps = boolValue;
                        break;
                    default:
                        break;
                }
            }
            if (isBlank)
            {
                switch (curLabel)
                {
                    case "minoffvalue":
                        MinOffValue = null;
                        break;
                    case "maxoffvalue":
                        MaxOffValue = null;
                        break;
                    case "feetperpage":
                        FeetPerPage = null;
                        break;
                    case "straightengpscommentdistance":
                        StraightenGpsCommentsDistance = null;
                        break;
                    default:
                        break;
                }
            }

        }

        public CisSettings() : base("Default Settings", FileType.CisSettings)
        {

        }

        public override bool IsEquivalent(File otherFile)
        {
            throw new NotImplementedException();
        }
    }
}
