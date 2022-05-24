using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace AccurateFileSystem.Dbf
{
    public class DbfFile
    {
        private byte[] RawData { get; set; }
        private int HeaderSize { get; set; }
        private int RecordSize { get; set; }
        private List<FieldDescriptor> FieldDescriptors { get; set; }
        private List<Record> Records { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public int Day { get; set; }
        public string FileName { get; set; }

        private DbfFile(string fileName, byte[] data)
        {
            FileName = FileName;
            RawData = data;
            var curByte = data[0];
            var version = curByte & 7;
            var hasDbaseIV = curByte.GetBit(3);
            var hasSql1 = curByte.GetBit(4);
            var hasSql2 = curByte.GetBit(5);
            var hasSql3 = curByte.GetBit(6);
            var hasMemo = curByte.GetBit(7);

            Year = data[1] + 2000;
            Month = data[2];
            Day = data[3];

            var records = data.GetInt32(true, 4);
            HeaderSize = data.GetInt16(true, 8);
            RecordSize = data.GetInt16(true, 10);

            var incomplete = data[14];
            var encryption = data[15];

            var mdx = data[28];
            var language = data[29];
            //var languageName = data.GetUtf8String(32, 32);
            data = data.Skip(32).ToArray();
            GetFieldDescriptor(data);

            data = data.Skip(HeaderSize - 32).ToArray();
            GetRecords(data);
        }

        public DbfFile(string fileName, List<FieldDescriptor> fields, List<Record> records)
        {
            FileName = fileName;
            FieldDescriptors = fields;
            Records = records;

            Year = 2021;
            Month = 6;
            Day = 3;
            CalculateSizes();
        }

        public async Task WriteToFile(StorageFolder folder)
        {
            var file = await folder.CreateFileAsync(FileName + ".dbf", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteBytesAsync(file, CreateBytes());
        }

        private void CalculateSizes()
        {
            HeaderSize = 32 + FieldDescriptors.Count * 32 + 1;
            RecordSize = GetRecordSize();
        }

        private int GetRecordSize()
        {
            var size = 1;

            foreach(var field in FieldDescriptors)
            {
                size += field.Length;
            }

            return size;
        }

        private byte[] CreateBytes()
        {
            var output = new byte[HeaderSize + (RecordSize * Records.Count) + 1];

            output[0] = 3;

            output[1] = (byte)(Year - 2000);
            output[2] = (byte)Month;
            output[3] = (byte)Day;

            Records.Count.ToBytes(true).CopyTo(output, 4);
            HeaderSize.ToBytes(true).CopyTo(output, 8);
            RecordSize.ToBytes(true).CopyTo(output, 10);

            CreateFieldBytes().CopyTo(output, 32);
            CreateRecordBytes().CopyTo(output, HeaderSize);
            output[output.Length - 1] = 0x1A;
            return output;
        }

        private byte[] CreateFieldBytes()
        {
            var output = new byte[HeaderSize - 32];

            var offset = 0;
            foreach(var field in FieldDescriptors)
            {
                var name = field.Name.PadRight(11, '\0');
                name.ToAsciiBytes().CopyTo(output, 0 + offset);
                output[11 + offset] = (byte)field.Type[0];
                field.DataAddress.ToBytes(true).CopyTo(output, 12 + offset);
                output[16 + offset] = (byte)field.Length;
                output[17 + offset] = (byte)field.DecimalCount;
                output[20 + offset] = (byte)field.WorkAreaId;
                output[23 + offset] = (byte)field.SetFieldsFlag;
                offset += 32;
            }
            output[HeaderSize - 33] = 13;
            return output;
        }

        private byte[] CreateRecordBytes()
        {
            var output = new byte[RecordSize * Records.Count];
            var offset = 0;

            foreach(var record in Records)
            {
                record.ToBytes().CopyTo(output, offset);
                offset += record.Length;
            }

            return output;
        }

        private void GetRecords(byte[] data)
        {
            Records = new List<Record>();
            while(data.Length != 0 && data[0] != 0x1A)
            {
                var record = FieldDescriptors.GetRecord(data);
                Records.Add(record);
                data = data.Skip(RecordSize).ToArray();
            }
        }

        private void GetFieldDescriptor(byte[] data)
        {
            FieldDescriptors = new List<FieldDescriptor>();
            while (data[0] != 0x0D)
            {
                var descriptor = new FieldDescriptor(data);
                FieldDescriptors.Add(descriptor);
                data = data.Skip(32).ToArray();
            }
        }

        public static async Task<DbfFile> GetDbfFile(StorageFile file)
        {
            byte[] result;
            using (var stream = await file.OpenStreamForReadAsync())
            using (var memoryStream = new MemoryStream())
            {

                stream.CopyTo(memoryStream);
                result = memoryStream.ToArray();
            }
            return new DbfFile(file.DisplayName, result);
        }

        public struct FieldDescriptor
        {
            public string Name { get; }
            public string Type { get; }
            public int Length { get; }
            public int DecimalCount { get; }
            public int WorkAreaId { get; }
            public int SetFieldsFlag { get; }
            public int DataAddress { get; }

            public FieldDescriptor(byte[] data)
            {
                Name = data.GetAsciiString(0, 11).Trim('\0');
                Type = data.GetAsciiString(11, 1);
                DataAddress = data.GetInt32(true, 12);
                Length = data[16];
                DecimalCount = data[17];
                WorkAreaId = data[20];
                SetFieldsFlag = data[23];
            }

            public FieldDescriptor(string name, string type, int length, int decimalCount)
            {
                Name = name;
                Type = type;
                Length = length;
                DecimalCount = decimalCount;

                DataAddress = 0;
                WorkAreaId = 0;
                SetFieldsFlag = 0;
            }

            public override string ToString()
            {
                return $"{Name} | {Type} | {Length}";
            }
        }

        public struct Record
        {
            public List<FieldDescriptor> FieldDescriptors { get; }
            public Dictionary<string, string> Values { get; }
            public bool IsDeleted { get; }
            public int Length { get; }

            public Record(List<FieldDescriptor> fieldDescriptors, Dictionary<string, string> values, bool isDeleted)
            {
                FieldDescriptors = fieldDescriptors;
                Values = values;
                IsDeleted = isDeleted;
                Length = 1;
                foreach(var field in FieldDescriptors)
                {
                    Length += field.Length;
                }
            }

            public byte[] ToBytes()
            {
                var output = new byte[Length];
                output[0] = (byte)' ';
                var offset = 1;
                foreach(var field in FieldDescriptors)
                {
                    var value = Values[field.Name] ?? "";
                    var padChar = field.Type == "N" ? '\0' : ' ';
                    value = value.PadRight(field.Length, padChar);
                    value.ToAsciiBytes().CopyTo(output, offset);
                    offset += field.Length;
                }

                return output;
            }
        }
    }
}
