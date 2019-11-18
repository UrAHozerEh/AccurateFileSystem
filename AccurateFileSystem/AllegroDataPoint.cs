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
        public int Id { get; }
        public double Footage { get; private set; }
        public double On { get; set; }
        public double MirOnOffset { get; set; }
        public double MirOn => On - MirOnOffset;
        public double? MirOnPerFoot { get; set; } = null;
        public double Off { get; set; }
        public double MirOffOffset { get; set; }
        public double MirOff => Off - MirOffOffset;
        public double? MirOffPerFoot { get; set; } = null;
        public double? Depth { get; private set; }
        public string OriginalComment { get; private set; }
        public string CommentTemplate { get; private set; }
        public BasicGeoposition GPS { get; private set; }
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
        public bool IsHiLo { get; private set; } = false;
        public List<TestStationRead> TestStationReads = new List<TestStationRead>();
        public bool HasReconnect { get; private set; } = false;

        public AllegroDataPoint(int id, double footage, double on, double off, BasicGeoposition gps, List<DateTime> times, string comment)
        {
            Id = id;
            Footage = footage;
            On = on;
            MirOnOffset = 0;
            Off = off;
            MirOffOffset = 0;
            OriginalComment = comment;
            GPS = gps;
            Times = times;
            ParseComment();
        }

        private void ParseComment()
        {
            if (string.IsNullOrWhiteSpace(OriginalComment))
                return;
            string docPattern = "(?i)DOC (\\d+)(in)?";
            var doc = Regex.Match(OriginalComment, docPattern);
            if (doc.Success)
            {
                Depth = double.Parse(doc.Groups[1].Value);
                OriginalComment = OriginalComment.Replace(doc.Value, "");
                OriginalComment = Regex.Replace(OriginalComment, "^\\s*,\\s*", "");
                OriginalComment = Regex.Replace(OriginalComment, "\\s*,\\s*$", "");
            }

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
                    if (read is ReconnectTestStationRead)
                        HasReconnect = true;
                    TestStationReads.Add(read);
                    ++count;
                }
            }
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
    }
}
