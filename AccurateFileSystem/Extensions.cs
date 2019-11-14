using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Foundation;

namespace AccurateFileSystem
{
    public static class Extensions
    {
        public static double Distance(this BasicGeoposition pos1, BasicGeoposition pos2)
        {
            double R = 3960 * 5280;
            double dLat = ToRadian(pos2.Latitude - pos1.Latitude);
            double dLon = ToRadian(pos2.Longitude - pos1.Longitude);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadian(pos1.Latitude)) * Math.Cos(ToRadian(pos2.Latitude)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
            double d = R * c;
            return d;
        }

        private static double ToRadian(double value)
        {
            return Math.PI * value / 180.0; ;
        }

        public static GeoboundingBox CombineAreas (this GeoboundingBox rect1, GeoboundingBox rect2)
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
    }
}
