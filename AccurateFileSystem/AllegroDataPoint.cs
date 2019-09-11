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
        public double On { get; private set; }
        public double MIROn { get; private set; }
        public double Off { get; private set; }
        public double MIROff { get; private set; }
        public string OriginalComment { get; private set; }
        public string CommentTemplate { get; private set; }
        public BasicGeoposition GPS { get; private set; }
        public bool HasGPS => GPS.Equals(new BasicGeoposition());
        public List<DateTime> Times { get; private set; }
        public bool HasTime => Times.Count != 0;
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
        public AllegroDataPoint(int id, double footage, double on, double off, BasicGeoposition gps, List<DateTime> times, string comment)
        {
            Id = id;
            Footage = footage;
            On = on;
            Off = off;
            OriginalComment = comment;
            GPS = gps;
            Times = times;
            ParseComment();
        }

        private void ParseComment()
        {
            if (string.IsNullOrEmpty(OriginalComment))
                return;
            string testStationPattern = @"\([^\)]+\)";
            var matches = Regex.Matches(OriginalComment, testStationPattern);
            CommentTemplate = OriginalComment;
            int count = 0;
            foreach (Match match in matches)
            {
                string tsString = match.Value;
                if(tsString == "(HILO)")
                {
                    IsHiLo = true;
                    continue;
                }
                string tsId = $"$${count}$$";
                CommentTemplate = CommentTemplate.Replace(tsString, tsId);
                TestStationRead read = TestStationReadFactory.GetRead(tsString, tsId);
                if (read == null)
                    continue;
            }
        }
    }
}
