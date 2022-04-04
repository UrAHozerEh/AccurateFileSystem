using AccurateFileSystem.Xml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace AccurateFileSystem.Kmz
{
    public class KmlFile : GeneralXmlFile
    {
        public KmlFile(string name, List<(double Footage, BasicGeoposition Gps, string Comment)> data) : base(name)
        {
            var kmlObject = new XmlObject("kml");
            kmlObject.Settings.Add("xmlns", "http://www.opengis.net/kml/2.2");
            kmlObject.Settings.Add("xmlns:gx", "http://www.google.com/kml/ext/2.2");
            kmlObject.Settings.Add("xmlns:atom", "http://www.w3.org/2005/Atom");
            kmlObject.Settings.Add("xmlns:kml", "http://www.opengis.net/kml/2.2");

            var documentObject = new XmlObject("Document");
            documentObject.Children.Add(new XmlObject("name", name));
            documentObject.Children.Add(new XmlObject("open", "0"));

            var styleMapObject = new XmlObject("StyleMap");
            styleMapObject.Settings.Add("id", "pointStyle");

            var styleMapPair = new XmlObject("Pair");
            styleMapPair.Children.Add(new XmlObject("key", "normal"));
            styleMapPair.Children.Add(new XmlObject("styleUrl", "normalPoint"));
            styleMapObject.Children.Add(styleMapPair);

            styleMapPair = new XmlObject("Pair");
            styleMapPair.Children.Add(new XmlObject("key", "highlight"));
            styleMapPair.Children.Add(new XmlObject("styleUrl", "highlightPoint"));
            styleMapObject.Children.Add(styleMapPair);
            documentObject.Children.Add(styleMapObject);

            // Normal Style
            var normalStyle = new XmlObject("Style");
            normalStyle.Settings.Add("id", "normalPoint");
            var normalIconStyle = new XmlObject("IconStyle");
            var normalIcon = new XmlObject("Icon");
            normalIcon.Children.Add(new XmlObject("href", "http://maps.google.com/mapfiles/kml/shapes/placemark_circle.png"));
            normalIconStyle.Children.Add(normalIcon);
            normalStyle.Children.Add(normalIconStyle);

            var normalLabelStyle = new XmlObject("LabelStyle");
            normalLabelStyle.Children.Add(new XmlObject("color", "00ffffff"));
            normalStyle.Children.Add(normalLabelStyle);
            documentObject.Children.Add(normalStyle);

            // Highlight Style
            var highlightStyle = new XmlObject("Style");
            highlightStyle.Settings.Add("id", "highlightPoint");
            var highlightIconStyle = new XmlObject("IconStyle");
            var highlightIcon = new XmlObject("Icon");
            highlightIcon.Children.Add(new XmlObject("href", "http://maps.google.com/mapfiles/kml/shapes/placemark_circle_highlight.png"));
            highlightIconStyle.Children.Add(highlightIcon);
            highlightStyle.Children.Add(highlightIconStyle);

            var highlightLabelStyle = new XmlObject("LabelStyle");
            highlightLabelStyle.Children.Add(new XmlObject("color", "00ffffff"));
            highlightStyle.Children.Add(highlightLabelStyle);
            documentObject.Children.Add(highlightStyle);

            foreach (var (footage, gps, comment) in data)
            {
                var placemark = new XmlObject("Placemark");
                placemark.Children.Add(new XmlObject("styleUrl", "#pointStyle"));
                placemark.Children.Add(new XmlObject("name", $"{footage:F0}"));
                placemark.Children.Add(new XmlObject("description", comment));
                var point = new XmlObject("Point");
                point.Children.Add(new XmlObject("coordinates", $"{gps.Longitude:F8},{gps.Latitude:F8},0"));
                placemark.Children.Add(point);
                documentObject.Children.Add(placemark);
            }

            kmlObject.Children.Add(documentObject);
            Objects.Add(kmlObject);
        }
    }
}
