using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Foundation;

namespace AccurateFileSystem
{
    public class AllegroCISFile : File, ISurveyFile
    {
        public Dictionary<string, string> Header { get; private set; }
        public Dictionary<int, AllegroDataPoint> Points { get; private set; }
        public string Extension { get; }
        public double StartFootage { get; private set; }
        public double EndFootage { get; private set; }
        public List<(int start, int end)> Reconnects { get; private set; }
        public bool IsReverseRun { get; set; }

        public AllegroCISFile(string name, string extension, Dictionary<string, string> header, Dictionary<int, AllegroDataPoint> points, FileType type) : base(name, type)
        {
            Header = header;
            Points = points;
            Extension = extension;
            if (Points.Count == 0)
                return;
            StartFootage = points[0].Footage;
            EndFootage = points[points.Count - 1].Footage;
            IsReverseRun = Name.ToLower().Contains("rev");
            ProcessPoints();
        }

        public override string ToString()
        {
            return $"'{Name}' cis with {Points.Count} points";
        }

        public List<(double Footage, bool IsReverseRun)> GetDirectionData()
        {
            var output = new List<(double, bool)>();
            foreach (var point in Points.Values)
            {
                output.Add((point.Footage, point.IsReverseRun));
            }
            return output;
        }

        /// <summary>
        /// Function used to clean up the data points in a file. This will also compute the MIR and related data for each point.
        /// This will also clear any duplicated more than once GPS points. May leave many blank GPS points, so you should do something to correct for those.
        /// </summary>
        private void ProcessPoints()
        {
            Reconnects = new List<(int, int)>();
            if (Points == null || Points.Count == 0)
                return;
            int incVal = int.Parse(Header["autoincval"]);
            int startIndex = 0;
            double? prevOn = null;
            double? prevOff = null;
            for (int i = 1; i < Points.Count; ++i)
            {
                var cur = Points[i];
                if(cur.On == 0 || cur.Off == 0)
                {
                    if(string.IsNullOrWhiteSpace(cur.OriginalComment))
                    {
                        if(i == Points.Count -1)
                        {
                            Points.Remove(i);
                            EndFootage = Points[Points.Count - 1].Footage;
                        }
                    }
                    if (cur.HasReconnect)
                    {
                        var recon = cur.GetReconnect();
                        cur.On = recon.NGOn;
                        cur.Off = recon.NGOff;
                    }
                    else if(prevOn.HasValue)
                    {
                        cur.On = prevOn.Value;
                        cur.Off = prevOff.Value;
                    }
                    else if(i + 1 < Points.Count)
                    {
                        cur.On = Points[i + 1].On;
                        cur.Off = Points[i + 1].Off;
                    }
                }
                prevOn = cur.On;
                prevOff = cur.Off;
                if (cur.HasReconnect)
                {
                    Reconnects.Add((startIndex, i));
                    startIndex = i;
                }
            }

            foreach (var (start, end) in Reconnects)
            {
                var startPoint = Points[start];
                var endPoint = Points[end];
                var footDist = endPoint.Footage - startPoint.Footage;

                ReconnectTestStationRead reconnect = endPoint.GetReconnect();
                reconnect.StartPoint = startPoint;
                reconnect.EndPoint = endPoint;

                var mirOnPerFoot = reconnect.MirOn / footDist;
                var mirOffPerFoot = reconnect.MirOff / footDist;

                for (int i = start; i < end; ++i)
                {
                    var curPoint = Points[i];
                    curPoint.NextReconnect = reconnect;
                    var curDist = curPoint.Footage - startPoint.Footage;

                    curPoint.MirOnOffset = Math.Round(mirOnPerFoot * curDist, 4);
                    curPoint.MirOffOffset = Math.Round(mirOffPerFoot * curDist, 4);

                    curPoint.MirOnPerFoot = mirOnPerFoot;
                    curPoint.MirOffPerFoot = mirOffPerFoot;
                }
            }
        }

        /*var cur = Points[i];
                var next = Points[i + 1];

                var footDist = next.Footage - cur.Footage;
                if (!cur.HasGPS || !next.HasGPS)
                    throw new Exception();
                var gpsDist = next.GPS.Distance(cur.GPS);
                if (gpsDist - footDist > incVal)
                    throw new Exception();
                    */

        /// <summary>
        /// Very explicit equals check. Will look at each read and make sure everything is equal in order to reduce the number of duplicate survey files.
        /// This should return true for any of the SVY, CSV, and BAK files from a single survey.
        /// Will also short circuit to true if GUID is equal, without checking anything further.
        /// </summary>
        /// <param name="otherFile">The other AllegroCISFile.</param>
        /// <returns>A bool stating if the two objects are equal.</returns>
        // TODO: Add a return for what the differences are.
        public override bool IsEquivalent(File otherFile)
        {
            var other = otherFile as AllegroCISFile;
            if (other == null)
                return false;
            // If Guid is equal then we know they are equal. Probably an uncommon check.
            if (other.Guid.Equals(Guid))
                return true;
            if (other.Name != Name)
                return false;
            if (other.Header.Count != Header.Count)
                return false;
            if (other.Points.Count != Points.Count)
                return false;

            // Checking to make sure header key and values match. Since they have the same count you need only check one way.
            foreach (string key in Header.Keys)
                if (!other.Header.ContainsKey(key) || other.Header[key] != Header[key])
                    return false;

            // Checking to make sure point key and values match. Since they have the same count you need only check one way.
            foreach (int key in Points.Keys)
                if (!other.Points.ContainsKey(key) || !Points[key].Equals(other.Points[key]))
                    return false;

            // All of the counts, name, header, and points are equal.
            return true;
        }

        public List<(double footage, double value)> GetDoubleData(string fieldName, double startFootage, double endFootage)
        {
            throw new NotImplementedException();
        }

        public Type GetDataType(string fieldName)
        {
            throw new NotImplementedException();
        }

        public List<(string fieldName, Type fieldType)> GetFields()
        {
            var list = new List<(string fieldName, Type fieldType)>();
            list.Add(("On", typeof(double)));
            list.Add(("Off", typeof(double)));
            list.Add(("On Compensated", typeof(double)));
            list.Add(("Off Compensated", typeof(double)));
            list.Add(("Depth", typeof(double)));
            list.Add(("Comment", typeof(string)));
            return list;
        }

        public List<(double footage, double value)> GetDoubleData(string fieldName)
        {
            switch (fieldName)
            {
                case "On":
                    return GetOnData();
                case "Off":
                    return GetOffData();
                case "Depth":
                    return GetDepthData();
                default:
                    return null;
            }
        }

        private List<(double footage, double value)> GetOnData()
        {
            var list = new List<(double, double)>();
            for (int i = 0; i < Points.Count; ++i)
            {
                list.Add((Points[i].Footage, Points[i].On));
            }
            return list;
        }

        private List<(double footage, double value)> GetOffData()
        {
            var list = new List<(double, double)>();
            for (int i = 0; i < Points.Count; ++i)
            {
                list.Add((Points[i].Footage, Points[i].Off));
            }
            return list;
        }

        private List<(double footage, double value)> GetDepthData()
        {
            var list = new List<(double, double)>();
            for (int i = 0; i < Points.Count; ++i)
            {
                if (Points[i].Depth.HasValue)
                    list.Add((Points[i].Footage, Points[i].Depth.Value));
            }
            return list;
        }

        public List<ReconnectTestStationRead> GetReconnects()
        {
            var output = new List<ReconnectTestStationRead>();
            foreach (var point in Points.Values)
            {
                if (!point.HasReconnect)
                    continue;
                var reconnect = point.GetReconnect();
                if (reconnect != null)
                    output.Add(reconnect);
            }
            return output;
        }

        public List<(double footage, string value)> GetStringData(string fieldName)
        {
            var list = new List<(double, string)>();
            for (int i = 0; i < Points.Count; ++i)
            {
                list.Add((Points[i].Footage, Points[i].OriginalComment));
            }
            return list;
        }

        public List<(double footage, string value)> GetStringData(string fieldName, double startFootage, double endFootage)
        {
            throw new NotImplementedException();
        }

        public GeoboundingBox GetGpsArea()
        {
            double minLat = double.MaxValue;
            double minLong = double.MaxValue;
            double maxLat = double.MinValue;
            double maxLong = double.MinValue;

            foreach(var point in Points.Values)
            {
                if(point.HasGPS)
                {
                    var gps = point.GPS;
                    if (gps.Latitude < minLat)
                        minLat = gps.Latitude;
                    if (gps.Longitude < minLong)
                        minLong = gps.Longitude;
                    if (gps.Latitude > maxLat)
                        maxLat = gps.Latitude;
                    if (gps.Longitude > maxLong)
                        maxLong = gps.Longitude;
                }
            }

            var nwPoint = new BasicGeoposition()
            {
                Latitude = maxLat,
                Longitude = minLong
            };
            var sePoint = new BasicGeoposition()
            {
                Latitude = minLat,
                Longitude = maxLong
            };
            return new GeoboundingBox(nwPoint, sePoint);
        }

        public double OffsetDistance(int myIndex, AllegroCISFile otherFile, int otherIndex, double roundTo)
        {
            var prevPoint = otherFile.Points[otherIndex];
            int prevOffset = 1;
            double offsetShift = 0;
            while (!otherFile.Points[otherIndex].HasGPS)
            {
                var leftCheck = otherIndex - prevOffset;
                var leftPoint = leftCheck >= 0 ? otherFile.Points[leftCheck] : null;
                var leftDist = prevPoint.Footage - (leftPoint?.Footage ?? double.MinValue);
                var rightCheck = otherIndex - prevOffset;
                var rightPoint = rightCheck < otherFile.Points.Count ? otherFile.Points[rightCheck] : null;
                var rightDist = (rightPoint?.Footage ?? double.MaxValue) - prevPoint.Footage;
                if (leftPoint != null || rightPoint != null)
                {
                    if (leftDist < rightDist)
                    {
                        otherIndex = leftCheck;
                        offsetShift = -leftDist;
                    }
                    else
                    {
                        otherIndex = rightCheck;
                        offsetShift = +rightDist;
                    }
                }
                if (leftCheck < 0 && rightCheck > otherFile.Points.Count)
                    throw new InvalidOperationException("There is no GPS to calculate offset with!");
                ++prevOffset;
            }
            var prevGps = otherFile.Points[otherIndex].GPS;

            var myPoint = Points[myIndex];
            int myOffset = 1;
            double myOffsetShift = 0;
            while (!Points[myIndex].HasGPS)
            {
                var leftCheck = myIndex - myOffset;
                var leftPoint = leftCheck >= 0 ? Points[leftCheck] : null;
                var leftDist = myPoint.Footage - (leftPoint?.Footage ?? double.MinValue);
                var rightCheck = myIndex - myOffset;
                var rightPoint = rightCheck < Points.Count ? Points[rightCheck] : null;
                var rightDist = (rightPoint?.Footage ?? double.MaxValue) - myPoint.Footage;
                if (leftPoint != null || rightPoint != null)
                {
                    if (leftDist < rightDist)
                    {
                        myIndex = leftCheck;
                        myOffsetShift = leftDist;
                    }
                    else
                    {
                        myIndex = rightCheck;
                        myOffsetShift = -rightDist;
                    }
                }
                if (leftCheck < 0 && rightCheck > Points.Count)
                    throw new InvalidOperationException("There is no GPS to calculate offset with!");
                ++myOffset;
            }
            var myGps = Points[myIndex].GPS;

            var dist = myGps.Distance(prevGps) + offsetShift + myOffsetShift;
            int mult = (int)(dist / roundTo);

            var floor = mult * roundTo;
            var floorDist = Math.Abs(floor - dist);
            var ceil = (mult + 1) * roundTo;
            var ceilDist = Math.Abs(ceil - dist);
            return floorDist < ceilDist ? floor : ceil;
        }
    }
}
