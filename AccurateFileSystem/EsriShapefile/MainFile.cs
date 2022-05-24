using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Storage;

namespace AccurateFileSystem.EsriShapefile
{
    public class MainFile
    {
        public string FileName { get; set; }
        public int FileCode { get; set; }
        public int Length { get; set; }
        public int Version { get; set; }
        public int ShapeType { get; set; }
        public double XMin { get; set; }
        public double XMax { get; set; }
        public double YMin { get; set; }
        public double YMax { get; set; }
        public double ZMin { get; set; }
        public double ZMax { get; set; }
        public double MMin { get; set; }
        public double MMax { get; set; }
        public List<Record> Records { get; set; }
        private byte[] MainRawData { get; }
        private byte[] IndexRawData { get; }
        private string ProjectionFileString => "GEOGCS[\"GCS_WGS_1984\",DATUM[\"D_WGS_1984\",SPHEROID[\"WGS_1984\",6378137.0,298.257223563]],PRIMEM[\"Greenwich\",0.0],UNIT[\"Degree\",0.0174532925199433]]";

        private MainFile(string fileName, byte[] mainData, byte[] indexData)
        {
            FileName = fileName;
            MainRawData = mainData;
            IndexRawData = indexData;
            GetHeader(mainData);
            mainData = mainData.Skip(100).ToArray();
            Records = new List<Record>();
            while (mainData.Length != 0)
            {
                var record = new Record(mainData);
                Records.Add(record);
                mainData = mainData.Skip(record.TotalBytes).ToArray();
            }
        }

        public async Task WriteToFile(StorageFolder folder)
        {
            var mainFile = await folder.CreateFileAsync(FileName + ".shp", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteBytesAsync(mainFile, CreateMainBytes());
            var indexFile = await folder.CreateFileAsync(FileName + ".shx", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteBytesAsync(indexFile, CreateIndexBytes());
            var projectionFile = await folder.CreateFileAsync(FileName + ".prj", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(projectionFile, ProjectionFileString);
        }

        public MainFile(string fileName, List<BasicGeoposition> points)
        {
            FileName = fileName;
            FileCode = 9994;
            Version = 1000;
            ShapeType = 1;
            Records = new List<Record>();
            for (var i = 0; i < points.Count; ++i)
            {
                var point = points[i];
                Records.Add(new Record(i + 1, point));
            }
        }

        private int GetFileLength()
        {
            var output = 50;
            foreach (var record in Records)
            {
                output += record.TotalLength;
            }
            return output;
        }

        private void GetHeader(byte[] data)
        {
            FileCode = data.GetInt32(false, 0);

            Length = data.GetInt32(false, 24);
            Version = data.GetInt32(true, 28);

            ShapeType = data.GetInt32(true, 32);

            XMin = data.GetDouble(true, 36);
            YMin = data.GetDouble(true, 44);

            XMax = data.GetDouble(true, 52);
            YMax = data.GetDouble(true, 60);

            ZMin = data.GetDouble(true, 68);
            ZMax = data.GetDouble(true, 76);

            MMin = data.GetDouble(true, 84);
            MMax = data.GetDouble(true, 92);
        }

        private void SetBounds()
        {
            XMin = Records[0].Longitude;
            XMax = Records[0].Longitude;
            YMin = Records[0].Latitude;
            YMax = Records[0].Latitude;

            foreach (var record in Records)
            {
                var curLon = record.Longitude;
                if (curLon < XMin) XMin = curLon;
                if (curLon > XMax) XMax = curLon;
                var curLat = record.Latitude;
                if (curLat < YMin) YMin = curLat;
                if (curLat > YMax) YMax = curLat;
            }
        }

        private byte[] CreateMainBytes()
        {
            var getFileLength = GetFileLength();
            var output = new byte[getFileLength * 2];
            SetBounds();
            CreateMainHeaderBytes().CopyTo(output, 0);
            CreateMainBodyBytes().CopyTo(output, 100);
            return output;
        }

        private byte[] CreateMainHeaderBytes()
        {
            var output = new byte[100];
            FileCode.ToBytes(false).CopyTo(output, 0);
            var length = GetFileLength();
            length.ToBytes(false).CopyTo(output, 24);
            Version.ToBytes(true).CopyTo(output, 28);
            ShapeType.ToBytes(true).CopyTo(output, 32);

            XMin.ToBytes(true).CopyTo(output, 36);
            YMin.ToBytes(true).CopyTo(output, 44);
            XMax.ToBytes(true).CopyTo(output, 52);
            YMax.ToBytes(true).CopyTo(output, 60);
            ZMin.ToBytes(true).CopyTo(output, 68);
            ZMax.ToBytes(true).CopyTo(output, 76);
            MMin.ToBytes(true).CopyTo(output, 84);
            MMax.ToBytes(true).CopyTo(output, 92);
            return output;
        }

        private byte[] CreateMainBodyBytes()
        {
            var length = GetFileLength();
            var output = new byte[length * 2 - 100];

            var offset = 0;
            foreach (var record in Records)
            {
                record.CreateMainBytes().CopyTo(output, offset);
                offset += record.TotalBytes;
            }
            return output;
        }

        private byte[] CreateIndexHeaderBytes()
        {
            var output = new byte[100];
            FileCode.ToBytes(false).CopyTo(output, 0);
            var length = 50 + 4 * Records.Count;
            length.ToBytes(false).CopyTo(output, 24);
            Version.ToBytes(true).CopyTo(output, 28);
            ShapeType.ToBytes(true).CopyTo(output, 32);

            XMin.ToBytes(true).CopyTo(output, 36);
            YMin.ToBytes(true).CopyTo(output, 44);
            XMax.ToBytes(true).CopyTo(output, 52);
            YMax.ToBytes(true).CopyTo(output, 60);
            ZMin.ToBytes(true).CopyTo(output, 68);
            ZMax.ToBytes(true).CopyTo(output, 76);
            MMin.ToBytes(true).CopyTo(output, 84);
            MMax.ToBytes(true).CopyTo(output, 92);
            return output;
        }

        private byte[] CreateIndexBytes()
        {
            var fileLength = 100 + 8 * Records.Count;
            var output = new byte[fileLength];
            SetBounds();
            CreateIndexHeaderBytes().CopyTo(output, 0);
            CreateIndexBodyBytes().CopyTo(output, 100);
            return output;
        }

        private byte[] CreateIndexBodyBytes()
        {
            var length = 8 * Records.Count;
            var output = new byte[length];

            var mainOffset = 50;
            var offset = 0;
            foreach (var record in Records)
            {
                mainOffset.ToBytes(false).CopyTo(output, offset);
                record.ContentLength.ToBytes(false).CopyTo(output, offset + 4);
                offset += 8;
                mainOffset += record.ContentLength + 4;
            }
            return output;
        }

        public static async Task<MainFile> GetMainFile(StorageFile mainFile, StorageFile indexFile)
        {
            byte[] mainResult, indexResult;
            using (var stream = await mainFile.OpenStreamForReadAsync())
            using (var memoryStream = new MemoryStream())
            {

                stream.CopyTo(memoryStream);
                mainResult = memoryStream.ToArray();
            }

            using (var stream = await indexFile.OpenStreamForReadAsync())
            using (var memoryStream = new MemoryStream())
            {

                stream.CopyTo(memoryStream);
                indexResult = memoryStream.ToArray();
            }
            return new MainFile(mainFile.DisplayName, mainResult, indexResult);
        }

        public struct Record
        {
            public int Number { get; }
            public int ShapeType { get; }
            public double Longitude { get; }
            public double Latitude { get; }
            public int ContentLength => 10;
            public int TotalLength => ContentLength + 4;
            public int TotalBytes => ContentLength * 2 + 8;

            public Record(byte[] data)
            {
                Number = Extensions.PopInt32(ref data, false);
                var length = Extensions.PopInt32(ref data, false);
                ShapeType = Extensions.PopInt32(ref data, true);
                Longitude = Extensions.PopDouble(ref data, true);
                Latitude = Extensions.PopDouble(ref data, true);
                if (length != ContentLength)
                    throw new ArgumentException();
            }

            public Record(int number, BasicGeoposition gps)
            {
                Number = number;
                ShapeType = 1;
                Longitude = gps.Longitude;
                Latitude = gps.Latitude;
            }

            public byte[] CreateMainBytes()
            {
                var output = new byte[TotalBytes];
                Number.ToBytes(false).CopyTo(output, 0);
                ContentLength.ToBytes(false).CopyTo(output, 4);
                ShapeType.ToBytes(true).CopyTo(output, 8);
                Longitude.ToBytes(true).CopyTo(output, 12);
                Latitude.ToBytes(true).CopyTo(output, 20);
                return output;
            }

            public override string ToString()
            {
                return $"{Number} | {Latitude:F11} | {Longitude:F11}";
            }
        }
    }
}
