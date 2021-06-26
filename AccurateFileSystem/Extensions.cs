using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.Storage;

namespace AccurateFileSystem
{
    public static class Extensions
    {
        public static Dbf.DbfFile.Record GetRecord(this List<Dbf.DbfFile.FieldDescriptor> fieldDescriptors, byte[] data)
        {
            var values = new Dictionary<string, string>();
            var deletionChar = PopChar(ref data);
            bool isDeleted = deletionChar == '*';
            foreach (var field in fieldDescriptors)
            {
                var curValue = PopAsciiString(ref data, field.Length);
                curValue = curValue.Trim().Trim('\0');
                values.Add(field.Name, curValue);
            }
            return new Dbf.DbfFile.Record(fieldDescriptors, values, isDeleted);
        }

        public static string PopAsciiString(this BitArray data, int length)
        {
            var byteCount = (int)Math.Ceiling((double)length / 8);
            var bytes = new byte[byteCount];
            var curBits = new BitArray(length, false);
            for (int i = 0; i < length; ++i)
            {
                curBits[i] = data[i];
            }
            curBits.CopyTo(bytes, 0);
            return bytes.GetAsciiString();
        }

        public static byte[] ToAsciiBytes(this string value)
        {
            return Encoding.ASCII.GetBytes(value);
        }

        public static string GetUtf8String(this byte[] data, int startIndex, int length)
        {
            return Encoding.UTF8.GetString(data, startIndex, length);
        }

        public static string GetAsciiString(this byte[] data, int startIndex, int length)
        {
            return Encoding.ASCII.GetString(data, startIndex, length);
        }

        public static string GetAsciiString(this byte[] data)
        {
            return Encoding.ASCII.GetString(data);
        }

        public static char PopChar(ref byte[] data)
        {
            var output = (char)data[0];
            data = data.Skip(1).ToArray();
            return output;
        }

        public static string PopAsciiString(ref byte[] data, int length)
        {
            var output = data.GetAsciiString(0, length);
            data = data.Skip(length).ToArray();
            return output;
        }

        public static bool GetBit(this byte b, int bitNumber)
        {
            return ((b >> bitNumber) & 1) != 0;
        }

        public static double GetDouble(this byte[] data, bool isLittle, int startIndex)
        {
            var curData = new byte[8];
            for (int i = 0; i < 8; ++i)
            {
                curData[i] = data[i + startIndex];
            }
            if (BitConverter.IsLittleEndian != isLittle)
                Array.Reverse(curData);
            return BitConverter.ToDouble(curData, 0);
        }

        public static byte[] ToBytes(this double data, bool isLittle)
        {
            var output = BitConverter.GetBytes(data);
            if (BitConverter.IsLittleEndian != isLittle)
                Array.Reverse(output);
            return output;
        }

        public static byte[] ToBytes(this int data, bool isLittle)
        {
            var output = BitConverter.GetBytes(data);
            if (BitConverter.IsLittleEndian != isLittle)
                Array.Reverse(output);
            return output;
        }

        public static double PopDouble(ref byte[] data, bool isLittle)
        {
            var output = data.GetDouble(isLittle, 0);
            data = data.Skip(8).ToArray();
            return output;
        }

        public static int GetInt32(this byte[] data, bool isLittle, int startIndex)
        {
            var curData = new byte[4];
            for (int i = 0; i < 4; ++i)
            {
                curData[i] = data[i + startIndex];
            }
            if (BitConverter.IsLittleEndian != isLittle)
                Array.Reverse(curData);
            return BitConverter.ToInt32(curData, 0);
        }

        public static int GetInt16(this byte[] data, bool isLittle, int startIndex)
        {
            var curData = new byte[2];
            for (int i = 0; i < 2; ++i)
            {
                curData[i] = data[i + startIndex];
            }
            if (BitConverter.IsLittleEndian != isLittle)
                Array.Reverse(curData);
            return BitConverter.ToInt16(curData, 0);
        }

        public static int PopInt32(ref byte[] data, bool isLittle)
        {
            var output = data.GetInt32(isLittle, 0);
            data = data.Skip(4).ToArray();
            return output;
        }

        public static double ParseDegree(this string text)
        {
            var degreeIndex = text.IndexOf('�');
            var minuteIndex = text.IndexOf('\'');
            var secondIndex = text.IndexOf('"');
            var degreeString = text.Substring(0, degreeIndex);
            var degree = double.Parse(degreeString);
            var minuteString = text.Substring(degreeIndex + 1, minuteIndex - degreeIndex - 1);
            var minute = double.Parse(minuteString);
            var secondString = text.Substring(minuteIndex + 1, secondIndex - minuteIndex - 1);
            var second = double.Parse(secondString);
            var suffix = text.Substring(secondIndex + 1);

            var output = degree;
            output += minute / 60.0;
            output += second / 3600.0;
            output *= GetMultiplier(suffix);

            return output;
        }

        private static int GetMultiplier(string suffix)
        {
            switch (suffix)
            {
                case "N":
                case "E":
                    return 1;
            }
            return -1;
        }

        public static async Task<List<string>> GetLines(this StorageFile file)
        {
            var output = new List<string>();
            using (var stream = await file.OpenStreamForReadAsync())
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    output.Add(reader.ReadLine());
                }
            }
            return output;
        }

        public static double TotalDistance(this List<BasicGeoposition> points)
        {
            if (points.Count < 2)
                return 0;
            var total = 0.0;
            for (int i = 1; i < points.Count; ++i)
            {
                var last = points[i - 1];
                var cur = points[i];
                total += cur.Distance(last);
            }
            return total;
        }

        public static double Distance(this BasicGeoposition pos1, BasicGeoposition pos2)
        {
            double earthRadius = 3960 * 5280;
            double deltaLat = ToRadian(pos2.Latitude - pos1.Latitude);
            double deltaLon = ToRadian(pos2.Longitude - pos1.Longitude);
            double a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                Math.Cos(ToRadian(pos1.Latitude)) * Math.Cos(ToRadian(pos2.Latitude)) *
                Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
            double c = 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
            double distance = earthRadius * c;
            return distance;
        }

        public static BasicGeoposition MiddleTowards(this BasicGeoposition pos1, BasicGeoposition pos2)
        {
            var lat = (pos1.Latitude + pos2.Latitude) / 2;
            var lon = (pos1.Longitude + pos2.Longitude) / 2;
            return new BasicGeoposition() { Latitude = lat, Longitude = lon };
        }

        private static double ToRadian(double value)
        {
            return Math.PI * value / 180.0; ;
        }

        public static (double Footage, double Distance, double ExtrapolatedFootage, double ExtrapolatedDistance, BasicGeoposition Gps) AlignPoint(this List<(double, BasicGeoposition)> points, BasicGeoposition otherGps)
        {
            int closestIndex = 0;
            double closestDistance = double.MaxValue;
            double footage = 0;
            var closestGps = new BasicGeoposition();
            for (int i = 0; i < points.Count; ++i)
            {
                var (foot, gps) = points[i];
                var curDistance = gps.Distance(otherGps);
                if (curDistance < closestDistance)
                {
                    closestDistance = curDistance;
                    closestIndex = i;
                    footage = foot;
                    closestGps = gps;
                }
            }
            var startIndex = Math.Max(0, closestIndex - 1);
            var endIndex = Math.Min(points.Count - 1, closestIndex + 1);
            var extrapolatedDist = closestDistance;
            double extrapolatedFoot = footage;
            for (int i = startIndex; i < endIndex; ++i)
            {
                var (startFoot, startGps) = points[i];
                var (endFoot, endGps) = points[i + 1];
                var footDist = endFoot - startFoot;

                var latFactor = (endGps.Latitude - startGps.Latitude) / footDist;
                var lonFactor = (endGps.Longitude - startGps.Longitude) / footDist;
                for (int j = 1; j < footDist; ++j)
                {
                    var fakeGps = new BasicGeoposition()
                    {
                        Latitude = startGps.Latitude + latFactor * j,
                        Longitude = startGps.Longitude + lonFactor * j
                    };
                    var curDist = fakeGps.Distance(otherGps);
                    if (curDist < extrapolatedDist)
                    {
                        extrapolatedDist = curDist;
                        extrapolatedFoot = startFoot + j;
                    }
                }
            }
            return (footage, closestDistance, extrapolatedFoot, extrapolatedDist, closestGps);
        }

        public static GeoboundingBox CombineAreas(this GeoboundingBox rect1, GeoboundingBox rect2)
        {
            var minLong = Math.Min(rect1.NorthwestCorner.Longitude, rect2.NorthwestCorner.Longitude);
            var maxLat = Math.Max(rect1.NorthwestCorner.Latitude, rect2.NorthwestCorner.Latitude);
            var maxLong = Math.Max(rect1.SoutheastCorner.Longitude, rect2.SoutheastCorner.Longitude);
            var minLat = Math.Min(rect1.SoutheastCorner.Latitude, rect2.SoutheastCorner.Latitude);

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

        public static (double Distance, BasicGeoposition PointOnSegment) DistanceToSegment(this BasicGeoposition point, BasicGeoposition start, BasicGeoposition end)
        {
            var A = point.Latitude - start.Latitude;
            var B = point.Longitude - start.Longitude;
            var C = end.Latitude - start.Latitude;
            var D = end.Longitude - start.Longitude;

            var dot = A * C + B * D;
            var len_sq = C * C + D * D;
            var param = dot / len_sq;

            double xx, yy;

            if (param < 0)
            {
                xx = start.Latitude;
                yy = start.Longitude;
            }
            else if (param > 1)
            {
                xx = end.Latitude;
                yy = end.Longitude;
            }
            else
            {
                xx = start.Latitude + param * C;
                yy = start.Longitude + param * D;
            }

            var dx = point.Latitude - xx;
            var dy = point.Longitude - yy;
            var pointOnLine = new BasicGeoposition() { Latitude = xx, Longitude = yy };
            return (point.Distance(pointOnLine), pointOnLine);
        }

        public static (double Distance, BasicGeoposition PointOnSegment) DistanceToLine(this BasicGeoposition point, List<BasicGeoposition> points)
        {
            var closeDist = double.MaxValue;
            var closePoint = point;

            for(int i = 1; i < points.Count; ++i)
            {
                var start = points[i - 1];
                var end = points[i];
                var (curDist, curPoint) = point.DistanceToSegment(start, end);
                if(curDist < closeDist)
                {
                    closeDist = curDist;
                    closePoint = curPoint;
                }
            }

            return (closeDist, closePoint);
        }

        public static (double Distance, BasicGeoposition PointOnSegment) DistanceToLines(this BasicGeoposition point, List<List<BasicGeoposition>> lines)
        {
            var closeDist = double.MaxValue;
            var closePoint = point;

            foreach(var line in lines)
            {
                var (curDist, curPoint) = point.DistanceToLine(line);
                if (curDist < closeDist)
                {
                    closeDist = curDist;
                    closePoint = curPoint;
                }
            }

            return (closeDist, closePoint);
        }
    }
}
