using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace AccurateFileSystem
{
    public class AllegroDataPoint
    {
        public int Id { get; set; }
        public double Footage { get; set; }
        public double On { get; set; }
        public double MirOnOffset { get; set; }
        public double MirOn => On - MirOnOffset;
        public double? MirOnPerFoot { get; set; } = null;
        public double Off { get; set; }
        public double MirOffOffset { get; set; }
        public double MirOff => Off - MirOffOffset;
        public double? MirOffPerFoot { get; set; } = null;
        public double? Depth { get; set; }
        public string OriginalComment { get; set; }
        public string CommentTemplate { get; private set; }
        public string StrippedComment { get; set; }
        public BasicGeoposition GPS { get; set; }
        public bool HasGPS => !GPS.Equals(new BasicGeoposition());
        public List<DateTime> Times { get; private set; }
        public bool HasTime => Times.Count != 0;
        public ReconnectTestStationRead NextReconnect { get; set; }
        public bool IsReverseRun { get; set; } = false;
        public DateTime? OnTime
        {
            get
            {
                if (Times.Count > 0)
                    return Times[0];
                return null;
            }
        }
        public DateTime? OffTime
        {
            get
            {
                if (Times.Count == 0)
                    return null;
                return Times[Times.Count - 1];
            }
        }
        public double IndicationValue { get; set; } = double.NaN;
        public bool HasIndication => !double.IsNaN(IndicationValue);
        public double IndicationPercent { get; set; } = double.NaN;
        public bool IsHiLo { get; private set; } = false;
        public List<TestStationRead> TestStationReads = new List<TestStationRead>();
        public bool HasReconnect { get; private set; } = false;
        public bool IsCorrected { get; set; }

        public AllegroDataPoint(int id, double footage, double on, double off, BasicGeoposition gps, List<DateTime> times, double indicationValue, string comment, bool isCorrected)
        {
            Id = id;
            Footage = footage;
            On = on;
            MirOnOffset = 0;
            Off = off;
            MirOffOffset = 0;
            OriginalComment = comment.Replace("(250V Range)", "").Replace("250V Range","");
            GPS = gps;
            Times = times;
            IndicationValue = indicationValue;
            IsCorrected = isCorrected;
            ParseComment(true);
        }

        private void ParseComment(bool removeDoc)
        {
            if (string.IsNullOrWhiteSpace(OriginalComment))
                return;
            string docPattern = "(?i)DOC:?\\s?(\\d+)\\s?(in)?(\")?";
            string docFootPattern = @"(?i)DOC:?\s?(\d+)\s?ft";
            string docFootInchPattern = "(?i)DOC:?\\s?(\\d+)'(\\d*)\\s?(in)?(\")?";
            //string offsetPattern = "(?i)(begin)?(start)?(end)? ?offset";
            //var offset = Regex.Match(OriginalComment, offsetPattern);
            //if (offset.Success)
            //{
            //    if (offset.Groups.Count(g => g.Length > 0) == 1)
            //        OriginalComment = OriginalComment.Replace(offset.Value, "");
            //}
            var doc = Regex.Match(OriginalComment, docPattern);
            var docFoot = Regex.Match(OriginalComment, docFootPattern);
            var docFootInch = Regex.Match(OriginalComment, docFootInchPattern);
            if (docFootInch.Success)
            {
                var feet = double.Parse(docFootInch.Groups[1].Value);
                double inches = 0;
                double.TryParse(docFootInch.Groups[2].Value, out inches);
                Depth = feet * 12 + inches;
                if (removeDoc)
                {
                    OriginalComment = OriginalComment.Replace(docFootInch.Value, "");

                    OriginalComment = Regex.Replace(OriginalComment, "^\\s*,\\s*", "");
                    OriginalComment = Regex.Replace(OriginalComment, "\\s*,\\s*$", "");
                }
            }
            else if (docFoot.Success)
            {
                Depth = double.Parse(docFoot.Groups[1].Value) * 12;
                if (removeDoc)
                {
                    OriginalComment = OriginalComment.Replace(docFoot.Value, "");

                    OriginalComment = Regex.Replace(OriginalComment, "^\\s*,\\s*", "");
                    OriginalComment = Regex.Replace(OriginalComment, "\\s*,\\s*$", "");
                }
            }
            else if (doc.Success)
            {
                Depth = double.Parse(doc.Groups[1].Value);
                if (removeDoc)
                {
                    OriginalComment = OriginalComment.Replace(doc.Value, "");

                    OriginalComment = Regex.Replace(OriginalComment, "^\\s*,\\s*", "");
                    OriginalComment = Regex.Replace(OriginalComment, "\\s*,\\s*$", "");
                }
            }
            var vaultMatch = Regex.Match(OriginalComment, "(?i)valt");
            if (vaultMatch.Success)
                OriginalComment = Regex.Replace(OriginalComment, vaultMatch.Value, "Vault");

            if (string.IsNullOrWhiteSpace(OriginalComment))
                return;
            string testStationPattern = @"\([^\)]+\)";

            var matches = Regex.Matches(OriginalComment, testStationPattern);
            CommentTemplate = OriginalComment;

            int count = 0;
            foreach (Match match in matches)
            {
                string tsString = match.Value;
                if (tsString == "(HILO)")
                {
                    IsHiLo = true;
                    continue;
                }
                string tsId = $"$${count}$$";
                CommentTemplate = CommentTemplate.Replace(tsString, tsId);
                TestStationRead read = TestStationReadFactory.GetRead(tsString, tsId);
                if (read == null)
                    CommentTemplate = CommentTemplate.Replace(tsId, tsString);
                else
                {
                    if (read is ReconnectTestStationRead reconnect)
                    {
                        HasReconnect = true;
                        On = reconnect.NGOn;
                        Off = reconnect.NGOff;
                    }
                    TestStationReads.Add(read);
                    ++count;
                }
            }
            StrippedComment = Regex.Replace(CommentTemplate, @"\$\$\d*\$\$", "");
        }

        public bool Equals(AllegroDataPoint other)
        {
            if (Id != other.Id)
                return false;
            if (Footage != other.Footage)
                return false;
            if (On != other.On)
                return false;
            if (Off != other.Off)
                return false;
            if (OriginalComment.Replace('"', '\'') != other.OriginalComment.Replace('"', '\''))
                return false;
            if (!GPS.Equals(other.GPS) && !CloseEnough(GPS, other.GPS))
                return false;
            if (!double.IsNaN(IndicationPercent) || !double.IsNaN(other.IndicationPercent))
                if (IndicationPercent != other.IndicationPercent)
                    return false;
            if (Times.Count != other.Times.Count)
                return false;
            for (int i = 0; i < Times.Count; ++i)
                if (Times[i] != other.Times[i])
                    return false;
            return true;
        }

        private bool CloseEnough(BasicGeoposition gps1, BasicGeoposition gps2)
        {
            double diff;
            if (gps1.Altitude != gps2.Altitude)
                return false;
            diff = Math.Abs(gps1.Longitude - gps2.Longitude);
            if (diff > 0.0000001)
                return false;
            diff = Math.Abs(gps1.Latitude - gps2.Latitude);
            if (diff > 0.0000001)
                return false;
            return true;
        }

        public ReconnectTestStationRead GetReconnect()
        {
            foreach (var read in TestStationReads)
                if (read is ReconnectTestStationRead)
                    return read as ReconnectTestStationRead;
            return null;
        }

        public string ToStringCsv()
        {
            var output = new StringBuilder();

            output.Append($"{Footage:F3},  ,");
            var onString = $"{On:F4}".PadLeft(8, ' ');
            var offString = $"{Off:F4}".PadLeft(8, ' ');
            output.Append(onString + ',' + offString + ',');
            if (OnTime != null)
            {
                var onTimeString = " " + OnTime?.ToString("MM/dd/yyyy, HH:mm:ss.fff") + ",g,";
                output.Append(onTimeString);
            }
            if (OffTime != null)
            {
                var offTimeString = " " + OffTime?.ToString("MM/dd/yyyy, HH:mm:ss.fff") + ",g,";
                output.Append(offTimeString);
            }
            var latString = $"{GPS.Latitude:F8}".PadLeft(14, ' ');
            var lonString = $"{GPS.Longitude:F8}".PadLeft(14, ' ');
            var altString = $"{GPS.Longitude:F1}".PadLeft(6, ' ');
            output.Append($"{latString},{lonString},{altString}, 1.200, D,");
            output.Append($"\"{OriginalComment}\"");

            return output.ToString();
        }
    }
}
