using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Storage;

namespace AccurateFileSystem
{
    public class FileFactory
    {
        private StorageFile File;

        public FileFactory(StorageFile file)
        {
            File = file;
        }

        public async Task<File> GetFile()
        {
            var extension = File.FileType.ToLower();

            switch (extension)
            {
                case ".svy":
                case ".aci":
                case ".dcv":
                    return await GetAllegroFile();
                case ".csv":
                default:
                    throw new Exception();
            }
        }

        private async Task<File> GetAllegroFile()
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            string extension = File.FileType;
            string headerDelimiter;
            switch (extension)
            {
                case ".svy":
                case ".aci":
                case ".dcv":
                    headerDelimiter = "=";
                    break;
                case ".csv":
                    headerDelimiter = ",";
                    break;
                default:
                    throw new Exception();
            }
            using (var stream = await File.OpenStreamForReadAsync())
            using (var reader = new StreamReader(stream))
            {
                bool isHeader = true;
                string line = reader.ReadLine();
                if (line != "Start survey:") throw new Exception();
                while (!reader.EndOfStream)
                {
                    line = reader.ReadLine();
                    if (isHeader)
                    {
                        int firstDelimiter = line.IndexOf(headerDelimiter);
                        if (firstDelimiter == -1)
                            throw new Exception();
                        string key = line.Substring(0, firstDelimiter).Trim();
                        string value = line.Substring(firstDelimiter + 1).Trim();
                        if (key == "Records")
                        {
                            value = value.Replace(":", "");
                            isHeader = false;
                        }
                        header.Add(key, value);
                    }
                    else
                    {
                        AllegroDataPoint point;
                        if (headerDelimiter == "=")
                            point = ParseAllegroLineFromACI(line);
                        else
                            point = ParseAllegroLineFromCSV(line);
                    }
                }
            }
            throw new Exception();
        }

        private AllegroDataPoint ParseAllegroLineFromCSV(string line)
        {
            return null;
        }

        private AllegroDataPoint ParseAllegroLineFromACI(string line)
        {
            string firstPattern = @"$(\s+?[^\s]+)";
            return null;
        }
    }
}
