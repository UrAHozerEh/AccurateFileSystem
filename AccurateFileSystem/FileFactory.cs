﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Storage;
using AllegroWaveformPoint = AccurateFileSystem.AllegroWaveformFile.DataPoint;

namespace AccurateFileSystem
{
    public class FileFactory
    {
        private StorageFile File;
        private string FileName;

        public FileFactory(StorageFile file)
        {
            File = file;
            FileName = Regex.Replace(File.Name, @"(\.[^\.]{3})?\.[^\.]{3}$", "");
        }

        public async Task<File> GetFile()
        {
            var extension = File.FileType.ToLower();

            switch (extension)
            {
                case ".svy":
                case ".aci":
                case ".dvg":
                case ".bak":
                    if (await IsAllegroFile())
                        return await GetAllegroFile();
                    else
                        throw new Exception("File is corrupted Allegro File.");
                case ".csv":
                    if (await IsAllegroFile())
                        return await GetAllegroFile();
                    else
                        return null;
                case ".txt":
                    return await GetAllegroWaveform();
                case ".xlsx":
                case ".xls":
                case ".zip":
                case ".pdf":
                    return null;
                default:
                    throw new Exception($"File is an unknown file format '{extension}'");
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
                string line = reader.ReadLine().Trim();
                DateTime time = new DateTime(0);
                int sampleRate = 0;
                string remark = "";
                string range = "";
                while (!string.IsNullOrEmpty(line))
                {
                    if (!line.Contains(':'))
                        throw new Exception($"Expected header labels in waveform file. Got '{line}' instead. Filename: '{FileName}'");
                    string label = line.Substring(0, line.IndexOf(':')).Trim();
                    string value = line.Substring(line.IndexOf(':') + 1).Trim();

                    switch (label)
                    {
                        case "Time":
                            time = DateTime.Parse(value);
                            break;
                        case "Range":
                            range = value;
                            break;
                        case "Remark":
                            remark = value;
                            break;
                        case "Sample rate":
                            sampleRate = int.Parse(value);
                            break;
                        default:
                            throw new Exception($"Unexpected label in Waveform header. Filename: '{FileName}' Label: '{label}'");
                    }

                    line = reader.ReadLine().Trim();
                }
                var points = new List<AllegroWaveformPoint>();
                while (!reader.EndOfStream)
                {
                    line = reader.ReadLine().Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    string[] split = line.Split(' ');
                    if (split.Length != 4 && split.Length != 1)
                        throw new Exception();
                    double value = double.Parse(split[0]);
                    if (split.Length == 1)
                    {
                        points.Add(new AllegroWaveformPoint(value));
                    }
                    else
                    {
                        bool on = split[1] == "1";
                        bool second = split[2] == "1";
                        bool third = split[3] == "1";
                        points.Add(new AllegroWaveformPoint(value, on, second, third));
                    }
                }
                if (time == new DateTime(0))
                    throw new Exception();
                var output = new AllegroWaveformFile(FileName, points, time, sampleRate, range, remark);
                return output;
            }
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
                case ".dvg":
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
                int lineCount = 0;
                bool isHeader = true;
                string line = reader.ReadLine();
                ++lineCount;
                if (!line.Contains("Start survey:")) throw new Exception();
                Match extraCommasMatch = Regex.Match(line, ",+");
                string extraCommasShort = extraCommasMatch.Success ? extraCommasMatch.Value.Substring(1) : "";
                while (!reader.EndOfStream)
                {
                    line = reader.ReadLine().Trim();
                    ++lineCount;
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
                        (key, value) = FixHeaderKeyValue(key, value);
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
            if (header.ContainsKey("survey_type"))
            {
                if (header["survey_type"] == "DC Survey")
                {
                    if (header.ContainsKey("onoff"))
                    {
                        if (header["onoff"] == "T")
                            type = FileType.OnOff;
                        else
                            type = FileType.Native;
                    }
                }
                else if (header["survey_type"] == "DCVG Survey")
                {
                    type = FileType.DCVG;
                }
            }
            var output = new AllegroCISFile(FileName, extension, header, points, type);
            return output;
        }

        private (string, string) FixHeaderKeyValue(string key, string value)
        {
            if (key == "Onoff_begins_w_ON")
                return ("Onoff_cycle_begins_with_ON", value);
            if (key == "ON_time")
                return ("Onoff_ON_time", value);
            if (key == "OFF_time")
                return ("Onoff_OFF_time", value);
            if (key == "version" && value.Length != 9)
                return (key, value.PadLeft(9, '0'));
            if (key == "date" && value.Length != 10)
            {
                var split = value.Split('/');
                split[0] = split[0].PadLeft(2, '0');
                split[1] = split[1].PadLeft(2, '0');
                return (key, $"{split[0]}/{split[1]}/{split[2]}");
            }
            return (key, value);
        }

        private AllegroDataPoint ParseAllegroLineFromCSV(int id, string line)
        {
            var split = line.Split(',');
            if (split.Length < 16)
                return null;
            var comment = split[15];
            if (split.Length > 16)
            {
                var sublist = split.Skip(15);
                comment = string.Join(",", sublist);
            }
            if (comment == "\"\"")
                comment = "";
            if (comment.Contains('"'))
                comment = Regex.Replace(comment, "^\"(.*)\"$", "$1");

            double footage = double.Parse(split[0]);
            double on = double.Parse(split[2]);
            double off = double.Parse(split[3]);

            List<DateTime> times = new List<DateTime>();
            if (IsValidTime(split, 4))
                times.Add(JoinAndParseDateTime(split, 4));
            if (IsValidTime(split, 7))
                times.Add(JoinAndParseDateTime(split, 7));

            double lat = double.Parse(split[10]);
            double lon = double.Parse(split[11]);
            double alt = double.Parse(split[12]);
            BasicGeoposition gps = new BasicGeoposition();
            if (lat != 0 && lon != 0)
            {
                gps = new BasicGeoposition
                {
                    Altitude = alt,
                    Longitude = lon,
                    Latitude = lat
                };
            }

            var output = new AllegroDataPoint(id, footage, on, off, gps, times, comment.Trim());
            return output;
        }

        private bool IsValidTime(string[] split, int start)
        {
            string date = split[start].Trim();
            if (date == "00/00/0000")
                return false;
            if (date == "01/01/1980")
                return false;
            if (string.IsNullOrWhiteSpace(date))
                return false;
            return true;
        }

        private DateTime JoinAndParseDateTime(string[] split, int start)
        {
            var joined = $"{split[start]},{split[start + 1]}".Trim();
            return ParseDateTime(joined);
        }

        private AllegroDataPoint ParseAllegroLineFromACI(int id, string line)
        {
            string firstPattern = @"^([^\s]+) M?\s+([^\s]+)\s+([^\s]+)";
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
                DateTime time = ParseDateTime(timeString);
                times.Add(time);
                line = line = Regex.Replace(line, timeMatch.Value, "").Trim();
            }
            var output = new AllegroDataPoint(id, footage, on, off, gps, times, line);
            return output;
        }

        private DateTime ParseDateTime(string input)
        {
            if (input.Contains('.'))
                return DateTime.ParseExact(input, "MM/dd/yyyy, HH:mm:ss.fff", CultureInfo.InvariantCulture);
            return DateTime.ParseExact(input, "MM/dd/yyyy, HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }
}
