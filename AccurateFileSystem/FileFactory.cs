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
                    {
                        var lines = await File.GetLines();
                        try
                        {
                            if (lines[0].StartsWith("Footage"))
                                return new GeneralCsv(File.DisplayName, lines);
                            if (lines[0].StartsWith("Record Type"))
                                return new Udl.UdlFile(File.DisplayName, lines);
                            if (lines[0].Contains("Preliminary"))
                                return new TidalCsvData(File.DisplayName, lines);
                            if (lines[1].Contains("Observations:"))
                                return new VivaxPcm(File.DisplayName, lines);
                            if ((lines[0].IndexOf("ID") <= 1 || lines[0].IndexOf("Pipeline") <= 1) && lines[0] != "Footage")
                                return new OtherPcm(File.DisplayName, lines);
                            return new GeneralCsv(File.DisplayName, lines);
                        }
                        catch
                        {
                            return new GeneralCsv(File.DisplayName, lines);
                        }
                        
                    }
                case ".txt":
                    return await GetAllegroWaveform();
                case ".regions2":
                    return await IitRegionFile.GetIitRegion(File);
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
                case ".jpg":
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
                var line = reader.ReadLine();
                return line.Contains("Start survey:");
            }
        }

        private async Task<AllegroWaveformFile> GetAllegroWaveform()
        {
            using (var stream = await File.OpenStreamForReadAsync())
            using (var reader = new StreamReader(stream))
            {
                var line = reader.ReadLine().Trim();
                var time = new DateTime(0);
                var sampleRate = 0;
                var remark = "";
                var range = "";
                while (!string.IsNullOrEmpty(line))
                {
                    if (!line.Contains(':'))
                        return null; //TODO: Handle this exception. There was a bad file in 4914
                    //throw new Exception($"Expected header labels in waveform file. Got '{line}' instead. Filename: '{FileName}'");
                    var label = line.Substring(0, line.IndexOf(':')).Trim();
                    var value = line.Substring(line.IndexOf(':') + 1).Trim();

                    switch (label)
                    {
                        case "Time":
                            value = value.Replace(", No GPS", "");
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
                    var split = line.Split(' ');
                    if (split.Length != 4 && split.Length != 1)
                        throw new Exception();
                    var value = double.Parse(split[0]);
                    if (split.Length == 1)
                    {
                        points.Add(new AllegroWaveformPoint(value));
                    }
                    else
                    {
                        var on = split[1] == "1";
                        var second = split[2] == "1";
                        var third = split[3] == "1";
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
            var header = new Dictionary<string, string>();
            var points = new Dictionary<int, AllegroDataPoint>();
            var extension = File.FileType.ToLower();
            string headerDelimiter;
            var pointId = 0;
            var noGaps = false;
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
            var type = FileType.Unknown;
            using (var stream = await File.OpenStreamForReadAsync())
            using (var reader = new StreamReader(stream))
            {
                var lineCount = 0;
                var isHeader = true;
                var line = reader.ReadLine();
                ++lineCount;
                if (!line.Contains("Start survey:")) throw new Exception();
                var extraCommasMatch = Regex.Match(line, ",,+");
                var extraCommasShort = extraCommasMatch.Success ? extraCommasMatch.Value.Substring(1) : "";
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
                        var firstDelimiter = line.IndexOf(headerDelimiter);
                        if (firstDelimiter == -1)
                            throw new Exception();
                        var key = line.Substring(0, firstDelimiter).Trim();
                        var value = line.Substring(firstDelimiter + 1).Trim();
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
                            point.Id += 1;
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
            var indicationMatch = Regex.Match(line, ",\"?[^\",]*\"?,\"?UN\"?,0,(\\d+\\.?\\d*),0");
            var indicationValue = double.NaN;
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
            var realDoC = double.NaN;
            if (split.Length > 16)
            {
                // Weird 111A stuff
                //if (split.Last().Contains("\""))
                //{
                //    for (int i = 15; i < split.Length; i++)
                //    {
                //        if (split[i].Contains("\""))
                //        {
                //            var sublistt = split.Skip(i);
                //            comment = string.Join(",", sublistt);
                //            if (!string.IsNullOrEmpty(split[i - 1]))
                //                realDoC = double.Parse(split[i - 1]);
                //            break;
                //        }
                //    }
                //}
                //else
                //{
                //    comment = split.Last();
                //    if (!string.IsNullOrEmpty(split[split.Length - 2]))
                //        realDoC = double.Parse(split[split.Length - 2]);
                //}
                // End Weird Stuffjknmbv
                // Start Normal Stuff
                var sublist = split.Skip(15);
                comment = string.Join(",", sublist);
                // End Normal Stuff
            }
            if (comment == "\"\"")
                comment = "";
            if (comment.Contains('"'))
                comment = Regex.Replace(comment, "^\"(.*)\"$", "$1");
            comment = Regex.Replace(comment, "\"\"", "\"");

            var footage = double.Parse(split[0]);
            var on = 0.0;
            if (!string.IsNullOrWhiteSpace(split[2]))
                on = double.Parse(split[2]);
            var off = 0.0;
            if (!string.IsNullOrWhiteSpace(split[3]))
                off = double.Parse(split[3]);

            var times = new List<DateTime>();
            if (IsValidTime(split, 4))
                times.Add(JoinAndParseDateTime(split, 4));
            if (IsValidTime(split, 7))
                times.Add(JoinAndParseDateTime(split, 7));
            double lat = 0, lon = 0, alt = 0;
            try
            {
                lat = double.Parse(split[10]);
                lon = double.Parse(split[11]);
                alt = string.IsNullOrWhiteSpace(split[12]) ? 0 : double.Parse(split[12]);
            }
            catch
            {

            }
            var dColumn = split[14].Trim();
            var gps = new BasicGeoposition();
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
            // Weird 111A Stuff
            //if (!double.IsNaN(realDoC))
            //    output.Depth = realDoC;
            // End Weird 111A Stuff
            return output;
        }

        private bool IsValidTime(string[] split, int start)
        {
            var date = split[start].Trim();
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
            var firstPattern = @"^([^\s]+) M?\s+([^\s]+)\s+([^\s]+)";
            var gpsPattern = @"\{(GD?E?) ([^\}]+)\}";
            var timePattern = @"\{T ([^g\}]+)g?\}";
            var indicationMatch = Regex.Match(line, "\\{Indication [^,]*, UN, 0, (\\d+\\.?\\d*), 0\\}");
            var indicationValue = double.NaN;
            if (indicationMatch.Success)
            {
                var commentIndicationMatch = Regex.Match(line, "\\(Indication [^;]*; [^\\)]+\\)");
                line = line.Replace(indicationMatch.Value, "").Replace(commentIndicationMatch.Value, "");
                indicationValue = double.Parse(indicationMatch.Groups[1].Value);
            }
            var match = Regex.Match(line, firstPattern);
            if (!match.Success)
                throw new Exception();
            var footage = double.Parse(match.Groups[1].Value);
            var on = double.Parse(match.Groups[2].Value);
            var off = double.Parse(match.Groups[3].Value);
            line = Regex.Replace(line, match.Value, "").Trim();
            match = Regex.Match(line, gpsPattern);
            var gps = new BasicGeoposition();
            var isCorrected = false;
            if (match.Success)
            {
                var gpsLabel = match.Groups[1].Value.ToLower();
                isCorrected = gpsLabel.Contains("d");
                if (!line.Contains("{g no gps}", StringComparison.OrdinalIgnoreCase))
                {
                    var split = match.Groups[2].Value.Split(',');
                    if (split.Length != 4)
                        throw new Exception();
                    var lat = double.Parse(split[0]);
                    var lon = double.Parse(split[1]);
                    var alt = double.Parse(split[2]);
                    gps = new BasicGeoposition
                    {
                        Altitude = alt,
                        Longitude = lon,
                        Latitude = lat
                    };
                }
                line = line = Regex.Replace(line, match.Value, "").Trim();

            }
            var timeMatches = Regex.Matches(line, timePattern);
            var times = new List<DateTime>();
            foreach (Match timeMatch in timeMatches)
            {
                var timeString = timeMatch.Groups[1].Value;
                var time = ParseDateTime(timeString);
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
