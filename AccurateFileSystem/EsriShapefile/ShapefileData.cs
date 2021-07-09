using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Storage;
using static AccurateFileSystem.Dbf.DbfFile;

namespace AccurateFileSystem.EsriShapefile
{
    public class ShapefileData
    {
        public List<FieldDescriptor> Fields { get; set; }
        public List<Record> Records { get; set; }
        public List<BasicGeoposition> Gps { get; set; }
        public string Name { get; set; }

        public ShapefileData(string name, List<string[]> data)
        {
            Name = name;
            var headers = data[0];
            CreateFields(headers);
            CreateRecords(data.Skip(1).ToList());
        }

        public async Task WriteToFolder(StorageFolder folder)
        {
            var outputFolder = await folder.CreateFolderAsync(Name, CreationCollisionOption.ReplaceExisting);
            var mainShapeFile = new MainFile(Name, Gps);
            await mainShapeFile.WriteToFile(outputFolder);
            var dbfFile = new Dbf.DbfFile(Name, Fields, Records);
            await dbfFile.WriteToFile(outputFolder);
        }

        private void CreateFields(string[] headers)
        {
            Fields = new List<FieldDescriptor>();
            foreach (var name in headers)
            {
                var type = "C";
                var length = 254;
                var decimals = 0;
                switch (name)
                {
                    case "STATION":
                    case "DEPTH":
                        length = 10;
                        type = "N";
                        break;
                    case "DATEOFCIS":
                        length = 8;
                        type = "D";
                        break;
                    case "LATITUDE":
                    case "LONGITUDE":
                    case "ONREAD":
                    case "OFFREAD":
                        length = 19;
                        decimals = 11;
                        type = "N";
                        break;
                    default:
                        break;
                }
                var field = new FieldDescriptor(name, type, length, decimals);
                Fields.Add(field);
            }
        }

        private void CreateRecords(List<string[]> data)
        {
            Records = new List<Record>();
            Gps = new List<BasicGeoposition>();
            foreach (var row in data)
            {
                var values = new Dictionary<string, string>();
                var lat = 0.0;
                var lon = 0.0;
                if (string.IsNullOrWhiteSpace(row[1])) continue;
                for (int i = 0; i < row.Length; ++i)
                {
                    var field = Fields[i];
                    var value = row[i] ?? "";
                    if (field.Name == "LATITUDE")
                        lat = double.Parse(value);
                    if (field.Name == "LONGITUDE")
                        lon = double.Parse(value);
                    if (field.Type == "D")
                    {
                        var split = value.Split('/');
                        var year = split[2];
                        var month = split[0].PadLeft(2, '0');
                        var day = split[1].PadLeft(2, '0');
                        value = year + month + day;
                    }
                    if (field.Type == "N")
                    {
                        value = value.PadLeft(1, '0');
                    }

                    values.Add(field.Name, value);
                }
                Gps.Add(new BasicGeoposition() { Latitude = lat, Longitude = lon });
                var record = new Record(Fields, values, false);
                Records.Add(record);
            }
        }
    }
}
