using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace AccurateFileSystem.Xml
{
    public class GeneralXmlFile
    {
        public string Name { get; set; }
        public List<XmlObject> Objects { get; set; } = new List<XmlObject>();
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();
        protected GeneralXmlFile(string name, string data)
        {
            Name = name;
            ParseData(data);
        }

        public GeneralXmlFile(string name)
        {
            Name = name;
            Settings.Add("version", "1.0");
            Settings.Add("encoding", "UTF-8");
        }

        public async Task WriteToFile(StorageFolder folder)
        {
            var file = await folder.CreateFileAsync(Name + ".kml", CreationCollisionOption.ReplaceExisting);
            var output = new StringBuilder();
            output.Append("<?xml");
            foreach (var (key, value) in Settings)
            {
                output.Append($" {key}=\"{value}\"");
            }
            output.AppendLine("?>");
            foreach (var obj in Objects)
            {
                output.AppendLine(obj.GetFileString());
            }
            await FileIO.WriteTextAsync(file, output.ToString());
        }

        private void ParseData(string data)
        {
            (_, Objects) = GetNextInfo(data);
        }

        public List<XmlObject> GetObjects(string name)
        {
            var output = new List<XmlObject>();

            foreach (var obj in Objects)
            {
                output.AddRange(obj.GetObjects(name));
            }

            return output;
        }

        private (string Value, List<XmlObject> Objects) GetNextInfo(string data)
        {
            var objects = new List<XmlObject>();
            if (data.IndexOf("<![CDATA") == 0)
                return (data, objects);
            if (!data.Contains('<'))
                return (data, objects);
            for (var i = 0; i < data.Length; ++i)
            {
                var curChar = data[i];
                var nextChar = (i < data.Length - 1) ? data[i + 1] : ' ';
                if (curChar != '<') continue;
                if (nextChar == '?')
                {
                    var endVersionIndex = data.IndexOf('?', i + 2);
                    var versionData = data.Substring(i + 2, endVersionIndex - (i + 2));
                    (_, Settings) = ParseHeaderData(versionData);
                    i = endVersionIndex + 1;
                    continue;
                }

                var nextI = i + 1;
                var endHeaderIndex = data.IndexOf('>', nextI);
                var headerData = data.Substring(nextI, endHeaderIndex - nextI);
                var (curName, curSettings) = ParseHeaderData(headerData);
                var body = GetBody(data, endHeaderIndex + 1, curName, out var nextEndIndex);
                if (curName == "Document")
                {
                    var dataPackage = new DataPackage();
                    dataPackage.RequestedOperation = DataPackageOperation.Copy;
                    dataPackage.SetText(body);
                    Clipboard.SetContent(dataPackage);
                }
                var (curValue, curChildren) = GetNextInfo(body);
                var curObject = new XmlObject(curName, curValue, curSettings, curChildren);
                objects.Add(curObject);
                i = nextEndIndex;
            }
            return ("", objects);
        }

        private string GetBody(string data, int startIndex, string objectName, out int endIndex)
        {
            endIndex = startIndex;
            var openString = $"<{objectName}";
            var closeString = $"</{objectName}>";
            var searchIndex = startIndex;
            var nextOpenIndex = data.IndexOf(openString, searchIndex);
            var nextCloseIndex = data.IndexOf(closeString, searchIndex);
            var level = 0;
            while ((nextOpenIndex != -1 && nextOpenIndex < nextCloseIndex) || level != 0)
            {
                if (nextOpenIndex == -1)
                    --level;
                else if (nextCloseIndex < nextOpenIndex)
                    --level;
                else
                    ++level;
                searchIndex = Math.Min(nextOpenIndex == -1 ? nextCloseIndex : nextOpenIndex, nextCloseIndex) + 1;
                nextOpenIndex = data.IndexOf(openString, searchIndex);
                nextCloseIndex = data.IndexOf(closeString, searchIndex);
            }
            endIndex = nextCloseIndex + closeString.Length;
            var bodyLength = nextCloseIndex - startIndex;
            if (bodyLength < 0)
                bodyLength = 0;
            return data.Substring(startIndex, bodyLength);
        }

        private (string Name, Dictionary<string, string> Settings) ParseHeaderData(string data)
        {
            var settings = new Dictionary<string, string>();
            var endNameIndex = data.Contains(' ') ? data.IndexOf(' ') : data.Length;
            var name = data.Substring(0, endNameIndex).Trim();
            var headerData = data.Substring(endNameIndex);

            var equalsIndex = headerData.IndexOf('=');
            while (equalsIndex != -1)
            {
                var key = headerData.Substring(0, equalsIndex).Trim();
                var endValueIndex = headerData.IndexOf('"', equalsIndex + 2);
                var valueLength = endValueIndex - (equalsIndex + 2);
                var value = headerData.Substring(equalsIndex + 2, valueLength);
                headerData = headerData.Substring(endValueIndex + 1);
                equalsIndex = headerData.IndexOf('=');
                settings.Add(key, value);
            }



            return (name, settings);
        }

        public static async Task<GeneralXmlFile> GetGeneralXml(StorageFile file)
        {
            var text = await FileIO.ReadTextAsync(file);
            return new GeneralXmlFile(file.DisplayName, text);
        }
    }
}
