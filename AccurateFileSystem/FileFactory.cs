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
                case ".acvg":
                case ".png":
                case ".regions":
                case ".kmz":
                case ".cor":
                case ".inf":
                    return null;
                default:
                    return null;
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
                        return null; //TODO: Handle this exception. There was a bad file in 4914
                    //throw new Exception($"Expected header labels in waveform file. Got '{line}' instead. Filename: '{FileName}'");
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
                            return null;
                            //throw new Exception($"Unexpected label in Waveform header. Filename: '{FileName}' Label: '{label}'");
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
            bool noGaps = true;
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
            FileType type = FileType.Unknown;
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
                double? startFoot = null;
                AllegroDataPoint lastPoint = null;
                
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
                        if (startFoot == null)
                            startFoot = point.Footage;
                        
                        if (extension != ".dvg")
                            point.Footage -= startFoot ?? 0;
                        if (noGaps && lastPoint != null && point.Footage - lastPoint.Footage > 20)
                        {
                            startFoot += point.Footage - lastPoint.Footage - 10;
                            point.Footage -= point.Footage - lastPoint.Footage - 10;
                        }
                        if (noGaps && lastPoint != null && type == FileType.OnOff && lastPoint.Footage + 20 == point.Footage)
                        {
                            var extrapolatedOn = (lastPoint.On + point.On) / 2;
                            var extrapolatedOff = (lastPoint.Off + point.Off) / 2;
                            var curGps = point.HasGPS ? point.GPS : lastPoint.GPS;
                            var extrapolatedPoint = new AllegroDataPoint(point.Id, lastPoint.Footage + 10, extrapolatedOn, extrapolatedOff, curGps, point.Times, point.IndicationValue, "", false);
                            point.Id = point.Id + 1;
                            points.Add(extrapolatedPoint.Id, extrapolatedPoint);
                            ++pointId;
                        }
                        points.Add(point.Id, point);
                        lastPoint = point;
                        ++pointId;
                    }
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
            var indicationMatch = Regex.Match(line, ",\"[^\"]*\",\"UN\",0,(\\d+\\.?\\d*),0");
            double indicationValue = double.NaN;
            if (indicationMatch.Success)
            {
                var commentIndicationMatch = Regex.Match(line, "\\(Indication [^;]*; [^\\)]+\\)");
                line = line.Replace(indicationMatch.Value, "").Replace(commentIndicationMatch.Value, "");
                indicationValue = double.Parse(indicationMatch.Groups[1].Value);
            }
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
            var dColumn = split[14].Trim();
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

            var output = new AllegroDataPoint(id, footage, on, off, gps, times, indicationValue, comment.Trim(), dColumn.ToLower().Contains("d"));
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
            string gpsPattern = @"\{(GD?E?) ([^\}]+)\}";
            string timePattern = @"\{T ([^g\}]+)g?\}";
            var indicationMatch = Regex.Match(line, "\\{Indication [^,]*, UN, 0, (\\d+\\.?\\d*), 0\\}");
            double indicationValue = double.NaN;
            if (indicationMatch.Success)
            {
                var commentIndicationMatch = Regex.Match(line, "\\(Indication [^;]*; [^\\)]+\\)");
                line = line.Replace(indicationMatch.Value, "").Replace(commentIndicationMatch.Value, "");
                indicationValue = double.Parse(indicationMatch.Groups[1].Value);
            }
            var match = Regex.Match(line, firstPattern);
            if (!match.Success)
                throw new Exception();
            double footage = double.Parse(match.Groups[1].Value);
            double on = double.Parse(match.Groups[2].Value);
            double off = double.Parse(match.Groups[3].Value);
            line = Regex.Replace(line, match.Value, "").Trim();
            match = Regex.Match(line, gpsPattern);
            BasicGeoposition gps = new BasicGeoposition();
            var isCorrected = false;
            if (match.Success)
            {
                var gpsLabel = match.Groups[1].Value.ToLower();
                isCorrected = gpsLabel.Contains("d");
                var split = match.Groups[2].Value.Split(',');
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
            var output = new AllegroDataPoint(id, footage, on, off, gps, times, indicationValue, line, isCorrected);
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
