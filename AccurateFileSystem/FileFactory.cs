using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
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
                case ".csv":
                case ".bak":
                    if (await IsAllegroFile())
                        return await GetAllegroFile();
                    else
                        throw new Exception();
                case ".txt":
                    return await GetAllegroWaveform();
                case ".xlsx":
                case ".xls":
                case ".zip":
                    return null;
                default:
                    throw new Exception();
            }
        }

        private async Task<bool> IsAllegroFile()
        {
            using (var stream = await File.OpenStreamForReadAsync())
            using (var reader = new StreamReader(stream))
            {
                string line = reader.ReadLine();
                return line.Contains("Start survey:");
            }
        }

        private async Task<AllegroWaveformFile> GetAllegroWaveform()
        {
            using (var stream = await File.OpenStreamForReadAsync())
            using (var reader = new StreamReader(stream))
            {
                string timeLine = reader.ReadLine().Replace("Time:","").Trim();
                DateTime time = DateTime.Parse(timeLine);
                string rangeLine = reader.ReadLine();
                string rateLine = reader.ReadLine();
                string remarkLine = reader.ReadLine();
                reader.ReadLine();
                var points = new List<AllegroWaveformFile.DataPoint>();
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine().Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    string[] split = line.Split(' ');
                    if (split.Length != 4)
                        throw new Exception();

                    double value = double.Parse(split[0]);
                    bool on = split[1] == "1";
                    bool second = split[2] == "1";
                    bool third = split[3] == "1";
                    points.Add(new AllegroWaveformFile.DataPoint
                    {
                        Value = value,
                        IsOn = on,
                        Second = second,
                        Third = third
                    });
                }
                for(int i = 0; i < points.Count; ++i)
                {

                }
            }
            return null;
        }

        private async Task<File> GetAllegroFile()
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            Dictionary<int, AllegroDataPoint> points = new Dictionary<int, AllegroDataPoint>();
            string extension = File.FileType.ToLower();
            string headerDelimiter;
            int pointId = 0;
            switch (extension)
            {
                case ".svy":
                case ".aci":
                case ".dcv":
                case ".bak":
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
                if (!line.Contains("Start survey:")) throw new Exception();
                Match extraCommasMatch = Regex.Match(line, ",+");
                string extraCommasShort = extraCommasMatch.Success ? extraCommasMatch.Value.Substring(1) : "";
                while (!reader.EndOfStream)
                {
                    line = reader.ReadLine().Trim();
                    if (isHeader)
                    {
                        if (extraCommasMatch.Success)
                            line = line.Replace(extraCommasShort, "");
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
                        if (extraCommasMatch.Success)
                            line = line.Replace(extraCommasMatch.Value, "");
                        if (line == "End survey" || string.IsNullOrEmpty(line))
                            continue;
                        AllegroDataPoint point;
                        if (headerDelimiter == "=")
                            point = ParseAllegroLineFromACI(pointId, line);
                        else
                            point = ParseAllegroLineFromCSV(pointId, line);

                        points.Add(pointId, point);
                        ++pointId;
                    }
                }
            }
            FileType type = FileType.Unknown;
            if (header.ContainsKey("onoff"))
            {
                if (header["onoff"] == "T")
                    type = FileType.OnOff;
            }
            string name = Regex.Replace(File.Name, @"(\.[^\.]{3})?\.[^\.]{3}$", "");
            var output = new AllegroCISFile(name, header, points, type);
            return output;
        }

        private AllegroDataPoint ParseAllegroLineFromCSV(int id, string line)
        {
            return null;
        }

        private AllegroDataPoint ParseAllegroLineFromACI(int id, string line)
        {
            string firstPattern = @"([^\s]+) M?\s? ([^\s]+)\s+([^\s]+)";
            string gpsPattern = @"\{GD?E? ([^\}]+)\}";
            string timePattern = @"\{T ([^g\}]+)g?\}";
            var match = Regex.Match(line, firstPattern);
            if (!match.Success)
                throw new Exception();
            double footage = double.Parse(match.Groups[1].Value);
            double on = double.Parse(match.Groups[2].Value);
            double off = double.Parse(match.Groups[3].Value);
            line = Regex.Replace(line, match.Value, "").Trim();
            match = Regex.Match(line, gpsPattern);
            BasicGeoposition gps = new BasicGeoposition();
            if (match.Success)
            {
                var split = match.Groups[1].Value.Split(',');
                if (split.Length != 4)
                    throw new Exception();
                double lat = double.Parse(split[0]);
                double lon = double.Parse(split[1]);
                double alt = double.Parse(split[2]);
                gps = new BasicGeoposition
                {
                    Altitude = alt,
                    Longitude = lon,
                    Latitude = lat
                };
                line = line = Regex.Replace(line, match.Value, "").Trim();
            }
            var timeMatches = Regex.Matches(line, timePattern);
            List<DateTime> times = new List<DateTime>();
            foreach (Match timeMatch in timeMatches)
            {
                var timeString = timeMatch.Groups[1].Value;
                DateTime time;
                if (timeString.Contains('.'))
                    time = DateTime.ParseExact(timeString, "MM/dd/yyyy, HH:mm:ss.fff", CultureInfo.InvariantCulture);
                else
                    time = DateTime.ParseExact(timeString, "MM/dd/yyyy, HH:mm:ss", CultureInfo.InvariantCulture);
                times.Add(time);
                line = line = Regex.Replace(line, timeMatch.Value, "").Trim();
            }
            var output = new AllegroDataPoint(id, footage, on, off, gps, times, line);
            return output;
        }
    }
}
