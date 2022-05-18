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
        public double TotalFootage => EndFootage - StartFootage;
        public List<(int start, int end)> Reconnects { get; private set; }
        public bool IsReverseRun { get; set; }
        public bool IsOnOff => !Header.ContainsKey("onoff") || Header["onoff"] == "T";

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

        public List<int> GetAnchorPoints(int distance = 50)
        {
            var testStations = new List<int> { 0 };
            var lastFoot = Points[0].Footage;
            for (int i = 1; i < Points.Count - 1; ++i)
            {
                var curPoint = Points[i];
                AllegroDataPoint nextPoint = Points[i + 1];
                if (curPoint.TestStationReads.Count != 0 && !Name.ToLower().Contains("redo"))
                {
                    if (i == 1 && curPoint.Footage - lastFoot <= 10)
                        continue;

                    if (nextPoint.TestStationReads.Count != 0)
                    {
                        if (nextPoint.Footage - curPoint.Footage <= 10)
                            continue;
                    }
                    testStations.Add(i);
                }
                else if (nextPoint.Footage - curPoint.Footage > distance)
                {
                    testStations.Add(i);
                }
                lastFoot = curPoint.Footage;
            }
            testStations.Add(Points.Count - 1);
            return testStations;
        }

        public override string ToString()
        {
            var typeString = "";
            switch (Type)
            {
                case FileType.OnOff:
                    typeString = "On Off";
                    break;
                case FileType.CISWaveform:
                    typeString = "Waveform";
                    break;
                case FileType.RISWaveform:
                    typeString = "RIS";
                    break;
                case FileType.DCVG:
                    typeString = "DCVG";
                    break;
                case FileType.ACVG:
                    typeString = "ACVG";
                    break;
                case FileType.PCM:
                    typeString = "PCM";
                    break;
                case FileType.SoilRes:
                    typeString = "Soil";
                    break;
                case FileType.Native:
                    typeString = "Native";
                    break;
                case FileType.Unknown:
                    typeString = "UNKNOWN";
                    break;
                default:
                    break;
            }
            return $"'{Name}' {typeString} with {Points.Count} points";
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

        public BasicGeoposition GetLastGps()
        {
            var lastGps = new BasicGeoposition();
            for (int i = 0; i < Points.Count; ++i)
            {
                if (Points[i].HasGPS)
                    lastGps = Points[i].GPS;
            }
            return lastGps;
        }

        public BasicGeoposition GetFirstGps()
        {
            for (int i = 0; i < Points.Count; ++i)
            {
                if (Points[i].HasGPS)
                    return Points[i].GPS;
            }
            return new BasicGeoposition();
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
            double startIrDrop = 0;
            double irDropFactor = 0;
            if (Type == FileType.DCVG)
            {
                var startOn = double.Parse(Header["DCVG_Begin_PS_ON"]);
                var startOff = double.Parse(Header["DCVG_Begin_PS_OFF"]);
                var endOn = double.Parse(Header["DCVG_End_PS_ON"]);
                var endOff = double.Parse(Header["DCVG_End_PS_OFF"]);

                startIrDrop = startOff - startOn;
                var endIrDrop = endOff - endOn;
                irDropFactor = (endIrDrop - startIrDrop) / TotalFootage;
            }
            int startIndex = 0;
            double? prevOn = null;
            double? prevOff = null;
            if (Points[0].HasReconnect)
            {
                var recon = Points[0].GetReconnect();
                recon.StartPoint = Points[0];
                recon.EndPoint = Points[0];
            }
            int lastUniqueGps = 0;
            int duplicateGpsCount = 0;
            int emptyGpsCount = 0;
            var prevGps = Points[0].GPS;
            for (int i = 1; i < Points.Count; ++i)
            {
                var cur = Points[i];
                if (cur.On == 0 || (cur.Off == 0 && IsOnOff))
                {
                    if (string.IsNullOrWhiteSpace(cur.OriginalComment))
                    {
                        if (i == Points.Count - 1)
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
                    else if (prevOn.HasValue)
                    {
                        cur.On = prevOn.Value;
                        cur.Off = prevOff.Value;
                    }
                    else if (i + 1 < Points.Count)
                    {
                        cur.On = Points[i + 1].On;
                        cur.Off = Points[i + 1].Off;
                    }
                }

                var curGps = cur.GPS;
                if (curGps.Equals(new BasicGeoposition()))
                {
                    ++emptyGpsCount;
                }
                else if (curGps.Equals(prevGps))
                {
                    ++duplicateGpsCount;
                }
                else
                {
                    if (duplicateGpsCount > 3 || emptyGpsCount != 0)
                    {
                        ExtrapolateGps(lastUniqueGps, i);
                    }
                    duplicateGpsCount = 0;
                    emptyGpsCount = 0;
                    lastUniqueGps = i;
                }

                if (cur.HasIndication)
                {
                    var curIrDrop = startIrDrop + (irDropFactor * cur.Footage);
                    cur.IndicationPercent = cur.IndicationValue / curIrDrop * 100;
                }
                prevOn = cur.On;
                prevOff = cur.Off;
                prevGps = cur.GPS;
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

        private void ExtrapolateGps(int from, int to)
        {
            var startPoint = Points[from];
            var startGps = startPoint.GPS;
            var startLat = startGps.Latitude;
            var startLon = startGps.Longitude;
            var startFootage = startPoint.Footage;

            var endPoint = Points[to];
            var endGps = endPoint.GPS;
            var endLat = endGps.Latitude;
            var endLon = endGps.Longitude;
            var endFootage = endPoint.Footage;

            var distance = endFootage - startFootage;
            var diffLat = endLat - startLat;
            var diffLon = endLon - startLon;

            var slopeLat = diffLat / distance;
            var slopeLon = diffLon / distance;

            for (int i = from + 1; i < to; ++i)
            {
                var curPoint = Points[i];
                var curFootage = curPoint.Footage;
                var curDistance = curFootage - startFootage;
                var curLat = Math.Round(curDistance * slopeLat + startLat, 8);
                var curLon = Math.Round(curDistance * slopeLon + startLon, 8);
                curPoint.GPS = new BasicGeoposition() { Latitude = curLat, Longitude = curLon };
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
            if (!(otherFile is AllegroCISFile other))
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
            list.Add(("DCVG", typeof(double)));
            list.Add(("Depth", typeof(double)));
            list.Add(("Comment", typeof(string)));
            return list;
        }

        public (double Distance, AllegroDataPoint Point) GetClosestPoint(BasicGeoposition pos, BasicGeoposition pos2)
        {
            var distance = double.MaxValue;
            AllegroDataPoint point = null;
            foreach (var cur in Points.Values)
            {
                if (cur.HasGPS)
                {
                    var curDist = cur.GPS.Distance(pos);
                    if (distance > curDist)
                    {
                        point = cur;
                        distance = curDist;
                    }
                    curDist = cur.GPS.Distance(pos2);
                    if (distance > curDist)
                    {
                        point = cur;
                        distance = curDist;
                    }
                }
            }

            return (distance, point);
        }

        public string ToStringCsv()
        {
            var output = new StringBuilder();

            output.AppendLine("Start survey:");

            foreach (var (key, value) in Header)
            {
                output.AppendLine($"{key},{value}");
            }
            for (int i = 0; i < Points.Count; ++i)
            {
                output.AppendLine(Points[i].ToStringCsv());
            }

            output.AppendLine("End survey");

            return output.ToString();
        }

        public List<(double Footage, double Value1, double Value2)> GetCombinedDoubleData(string name1, string name2)
        {
            var val1Data = GetDoubleData(name1);
            var val2Data = GetDoubleData(name2);
            var index1 = 0;
            var index2 = 0;
            var output = new List<(double, double, double)>();
            while (index1 < val1Data.Count && index2 < val2Data.Count)
            {

                var (foot1, value1) = index1 == val1Data.Count ? (double.NaN, double.NaN) : val1Data[index1];
                var (foot2, value2) = index2 == val2Data.Count ? (double.NaN, double.NaN) : val2Data[index2];
                if (foot1 == foot2)
                {
                    output.Add((foot1, value1, value2));
                    ++index1;
                    ++index2;
                }
                else if (foot1 < foot2)
                {
                    output.Add((foot1, value1, double.NaN));
                    ++index1;
                }
                else
                {
                    output.Add((foot1, double.NaN, value2));
                    ++index2;
                }
            }
            return output;
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
                case "DCVG":
                    return GetOnData();
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

            foreach (var point in Points.Values)
            {
                if (point.HasGPS)
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
                var rightCheck = otherIndex + prevOffset;
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
                var rightCheck = myIndex + myOffset;
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
