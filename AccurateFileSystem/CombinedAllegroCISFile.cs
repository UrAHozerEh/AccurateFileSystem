using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace AccurateFileSystem
{
    public class CombinedAllegroCISFile
    {
        public FileInfoLinkedList FileInfos { get; set; }
        public FileType Type { get; set; }
        public string Name { get; set; }
        public List<(double Footage, bool IsReverse, AllegroDataPoint Point, bool UseMir, AllegroCISFile File)> Points = null;

        private CombinedAllegroCISFile(string name, FileType type, FileInfoLinkedList fileInfos)
        {
            Type = type;
            Name = name;
            FileInfos = fileInfos;
            UpdatePoints();
            var output = FilterMir(new List<string>() { "anode", "rectifier" });
        }

        public List<(string fieldName, Type fieldType)> GetFields()
        {
            var list = new List<(string fieldName, Type fieldType)>
            {
                ("On", typeof(double)),
                ("Off", typeof(double)),
                ("On Compensated", typeof(double)),
                ("Off Compensated", typeof(double)),
                ("Depth", typeof(double)),
                ("Comment", typeof(string))
            };
            return list;
        }

        public void Reverse()
        {
            FileInfos = FileInfos.Reverse();
            FileInfos.CalculateOffset(10);
            UpdatePoints();
        }

        public string GetTabularData(int readDecimals = 4)
        {
            var output = new StringBuilder();
            var readFormat = $"F{readDecimals}";
            var curLine = new string[31];
            curLine[0] = "Footage";
            curLine[1] = "Compensated On";
            curLine[2] = "Compensated Off";
            curLine[3] = "On";
            curLine[4] = "Off";
            curLine[5] = "Date";
            curLine[6] = "Latitude";
            curLine[7] = "Longitude";
            curLine[8] = "Remarks";
            curLine[9] = "Near Ground On";
            curLine[10] = "Near Ground Off";
            curLine[11] = "Left On";
            curLine[12] = "Left Off";
            curLine[13] = "Right On";
            curLine[14] = "Right Off";
            curLine[15] = "Far Ground On";
            curLine[16] = "Far Ground Off";
            curLine[17] = "MIR On";
            curLine[18] = "MIR Off";
            curLine[19] = "Reconnect Distance";
            curLine[20] = "Reconnect Direction";
            curLine[21] = "Coupon On";
            curLine[22] = "Coupon Off";
            curLine[23] = "Coupon Native";
            curLine[24] = "Casing On";
            curLine[25] = "Casing Off";
            curLine[26] = "Foreign On";
            curLine[27] = "Foreign Off";
            curLine[28] = "ACV";
            curLine[29] = "Full Comment";
            curLine[30] = "DoC";
            output.AppendLine(string.Join("\t", curLine));

            foreach (var (footage, isReverse, point, useMir, file) in Points)
            {
                curLine = new string[31];
                curLine[0] = footage.ToString("F0");
                curLine[1] = point.MirOn.ToString(readFormat);
                curLine[2] = point.MirOff.ToString(readFormat);
                curLine[3] = point.On.ToString(readFormat);
                curLine[4] = point.Off.ToString(readFormat);
                if (point.HasTime)
                    curLine[5] = point.Times[0].ToString("MM/dd/yyyy");
                else
                    curLine[5] = "N/A";
                if (point.HasGPS)
                {
                    curLine[6] = point.GPS.Latitude.ToString("F8");
                    curLine[7] = point.GPS.Longitude.ToString("F8");
                }
                else
                {
                    curLine[6] = "N/A";
                    curLine[7] = "N/A";
                }
                curLine[8] = point.StrippedComment ?? "";
                curLine[29] = point.OriginalComment ?? "";
                if (point.TestStationReads.Count > 0)
                {
                    foreach (TestStationRead read in point.TestStationReads)
                    {
                        if (read is ReconnectTestStationRead reconnect)
                        {
                            curLine[9] = reconnect.NGOn.ToString(readFormat);
                            curLine[10] = reconnect.NGOff.ToString(readFormat);

                            curLine[15] = reconnect.FGOn.ToString(readFormat);
                            curLine[16] = reconnect.FGOff.ToString(readFormat);

                            curLine[17] = reconnect.MirOn.ToString(readFormat);
                            curLine[18] = reconnect.MirOff.ToString(readFormat);

                            curLine[19] = reconnect.ReconnectDistance.ToString("F0");
                            curLine[20] = isReverse ? "Downstream" : "Upstream";
                        }
                        else if (read is SingleTestStationRead single)
                        {
                            var lowerTag = single.Tag?.ToLower() ?? "";
                            if (lowerTag.Contains("coup"))
                            {
                                if (lowerTag.Contains("off"))
                                {
                                    curLine[22] = single.Off.ToString(readFormat);
                                }
                                else
                                {
                                    curLine[21] = single.On.ToString(readFormat);
                                }
                            }
                            else if (lowerTag.Contains("nat"))
                            {
                                curLine[23] = single.Off.ToString(readFormat);
                            }
                            else
                            {
                                if (string.IsNullOrEmpty(curLine[9]))
                                {
                                    curLine[9] = single.On.ToString(readFormat);
                                    curLine[10] = single.Off.ToString(readFormat);
                                }
                            }
                        }
                        else if (read is SideDrainTestStationRead sideDrain)
                        {
                            curLine[11] = sideDrain.LeftOn.ToString(readFormat);
                            curLine[12] = sideDrain.LeftOff.ToString(readFormat);

                            curLine[13] = sideDrain.RightOn.ToString(readFormat);
                            curLine[14] = sideDrain.RightOff.ToString(readFormat);
                        }
                        else if (read is CasingTestStationRead casing)
                        {
                            if (string.IsNullOrEmpty(curLine[9]))
                            {
                                curLine[9] = casing.StructOn.ToString(readFormat);
                                curLine[10] = casing.StructOff.ToString(readFormat);
                            }
                            curLine[24] = casing.CasingOn.ToString(readFormat);
                            curLine[25] = casing.CasingOff.ToString(readFormat);
                        }
                        else if (read is CrossingTestStationRead crossing)
                        {
                            if (string.IsNullOrEmpty(curLine[9]))
                            {
                                curLine[9] = crossing.StructOn.ToString(readFormat);
                                curLine[10] = crossing.StructOff.ToString(readFormat);
                            }
                            curLine[26] = crossing.ForeignOn.ToString(readFormat);
                            curLine[27] = crossing.ForeignOff.ToString(readFormat);
                        }
                        else if (read is ACTestStationRead ac)
                        {
                            curLine[28] = ac.Value.ToString(readFormat);
                        }
                    }
                    for (int i = 9; i <= 28; ++i)
                    {
                        if (string.IsNullOrEmpty(curLine[i]))
                            curLine[i] = "N/A";
                    }
                }
                curLine[30] = point.Depth?.ToString("F0") ?? "";
                output.AppendLine(string.Join("\t", curLine));
            }

            return output.ToString();
        }

        public string FilterMir(List<string> blacklist)
        {
            StringBuilder output = new StringBuilder();
            var alreadyFound = new Dictionary<ReconnectTestStationRead, (bool UseMir, double Start, double End, string Reason)>();
            for (int i = 0; i < Points.Count; ++i)
            {
                var (footage, isReverse, point, _, file) = Points[i];
                if (point.NextReconnect != null)
                {
                    var recon = point.NextReconnect;
                    if (!alreadyFound.ContainsKey(recon))
                    {
                        alreadyFound.Add(recon, (true, footage, footage, null));
                        for (int j = recon.StartPoint.Id; j <= recon.EndPoint.Id; ++j)
                        {
                            var curPoint = file.Points[j];
                            var (contains, rea) = ContainsFromList(curPoint.OriginalComment, blacklist);
                            if (contains)
                            {
                                alreadyFound[recon] = (false, footage, footage, rea);
                                break;
                            }
                        }
                    }
                    var (useMir, start, end, reason) = alreadyFound[recon];
                    Points[i] = (footage, isReverse, point, useMir, file);
                    if (footage < start)
                        alreadyFound[recon] = (useMir, footage, end, reason);
                    if (footage > end)
                        alreadyFound[recon] = (useMir, start, footage, reason);
                }
            }

            foreach (var (useMir, start, end, curReason) in alreadyFound.Values)
            {
                if (!useMir)
                {
                    var reasonCaps = (curReason == "anode" ? "Anode" : "Rectifier");
                    output.AppendLine($"{start},{end},{reasonCaps}");
                }
            }

            return output.ToString();
        }

        private (bool, string) ContainsFromList(string comment, List<string> list)
        {
            var commentToLower = comment.ToLower();
            foreach (string s in list)
            {
                if (commentToLower.Contains(s.ToLower()))
                {
                    return (true, s);
                }
            }
            return (false, null);
        }

        public List<(double footage, double value)> GetDoubleData(string fieldName)
        {
            switch (fieldName)
            {
                case "On":
                    return GetOnData();
                case "On Compensated":
                    return GetOnCompensatedData();
                case "Off":
                    return GetOffData();
                case "Off Compensated":
                    return GetOffCompensatedData();
                case "Depth":
                    return GetDepthData();
                default:
                    return null;
            }
        }

        public void UpdatePoints()
        {
            var list = new List<(double Footage, bool IsReverse, AllegroDataPoint Point, bool UseMir, AllegroCISFile File)>();
            var tempFileInfoNode = FileInfos;
            var offset = 0.0;
            while (tempFileInfoNode != null)
            {
                var info = tempFileInfoNode.Info;
                offset += info.Offset;
                var file = info.File;
                var fileOffset = info.StartFootage;
                var indexOffset = info.Start > info.End ? -1 : 1;
                var isReverse = info.Start > info.End;
                for (int i = info.Start; i != info.End + indexOffset; i += indexOffset)
                {
                    var curPoint = file.Points[i];
                    var footage = Math.Abs(curPoint.Footage - fileOffset) + offset;
                    if (curPoint.MirOff < curPoint.MirOn)
                    {
                        // 191-1
                        //curPoint.Off = list.Last().Point.Off;
                    }
                    if (footage == 1400)
                    {
                        // 3008-01
                        //curPoint.On = list.Last().Point.On;
                    }
                    if (curPoint.On > -1.3 && footage > 63200 && footage < 63300)
                    {
                        // 191-1
                        //curPoint.Off = list.Last().Point.Off;
                    }
                    if (footage == 58500)
                    {
                        // 191-1
                        //curPoint.On = list.Last().Point.On;
                    }
                    if (curPoint.On > -1.15)
                    {
                        // 191B
                        //curPoint.On = list.Last().Point.On;
                    }
                    if(curPoint.Off < -0.85)
                    {
                        // 3001-05
                        //curPoint.On = list.Last().Point.On;
                        //curPoint.Off = list.Last().Point.Off;
                    }
                    list.Add((footage, isReverse, curPoint, true, file));

                }
                offset += info.TotalFootage;
                tempFileInfoNode = tempFileInfoNode.Next;
            }
            // 3008-01
            //list.Last().Point.Off = -list.Last().Point.Off;
            Points = list;
        }

        public List<(double Footage, double On, double Off)> GetCombinedMirData()
        {
            var list = new List<(double, double, double)>();
            for (int i = 0; i < Points.Count; ++i)
            {
                var (footage, _, point, useMir, _) = Points[i];
                if (useMir)
                    list.Add((footage, point.MirOn, point.MirOff));
                else
                    list.Add((footage, point.On, point.Off));
            }
            return list;
        }

        private List<(double footage, double value)> GetOnData()
        {
            var list = new List<(double, double)>();
            for (int i = 0; i < Points.Count; ++i)
            {
                list.Add((Points[i].Footage, Points[i].Point.On));
            }
            return list;
        }

        private List<(double footage, double value)> GetOnCompensatedData()
        {
            var list = new List<(double, double)>();
            for (int i = 0; i < Points.Count; ++i)
            {
                var (footage, _, point, useMir, _) = Points[i];
                if (useMir)
                    list.Add((footage, point.MirOn));
                else
                    list.Add((footage, point.On));
            }
            return list;
        }

        private List<(double footage, double value)> GetOffData()
        {
            var list = new List<(double, double)>();
            for (int i = 0; i < Points.Count; ++i)
            {
                list.Add((Points[i].Footage, Points[i].Point.Off));
            }
            return list;
        }

        private List<(double footage, double value)> GetOffCompensatedData()
        {
            var list = new List<(double, double)>();
            for (int i = 0; i < Points.Count; ++i)
            {
                var (footage, _, point, useMir, _) = Points[i];
                if (useMir)
                    list.Add((footage, point.MirOff));
                else
                    list.Add((footage, point.Off));
            }
            return list;
        }

        private List<(double footage, double value)> GetDepthData()
        {
            var list = new List<(double, double)>();
            for (int i = 0; i < Points.Count; ++i)
            {
                if (Points[i].Point.Depth.HasValue)
                    list.Add((Points[i].Footage, Points[i].Point.Depth.Value));
            }
            return list;
        }

        public List<(double Footage, double OnMirPerFoot, double OffMirPerFoot, bool IsReverse)> GetReconnects()
        {
            var list = new List<(double, double, double, bool)>();
            for (int i = 0; i < Points.Count; ++i)
            {
                var point = Points[i].Point;
                if (Points[i].Point.MirOnPerFoot.HasValue)
                    list.Add((Points[i].Footage, point.MirOnPerFoot.Value, point.MirOffPerFoot.Value, Points[i].IsReverse));
            }
            return list;
        }

        public List<(double footage, string value)> GetCommentData(string fieldName)
        {
            var list = new List<(double, string)>();
            for (int i = 0; i < Points.Count; ++i)
            {
                list.Add((Points[i].Footage, Points[i].Point.OriginalComment));
            }
            return list;
        }

        public List<(double Footage, bool IsReverseRun)> GetDirectionData()
        {
            var output = new List<(double, bool)>();
            foreach (var point in Points)
            {
                output.Add((point.Footage, point.IsReverse));
            }
            return output;
        }

        public static CombinedAllegroCISFile CombineFiles(string name, List<AllegroCISFile> files)
        {
            var first = files.First();
            var type = first.Type;

            var calc = new OrderCalculator(files);
            calc.AsyncSolve();
            //TODO: Maybe look at TS MP to determine if we should reverse the new file.
            var allSolution = calc.GetAllUsedSolution();
            var startTS = allSolution.First.Info.File.Points[allSolution.First.Info.Start];
            var endTS = allSolution.Last.Info.File.Points[allSolution.Last.Info.End];

            var startTSMatch = Regex.Match(startTS.OriginalComment, @"(?i)mp ?(\d+)");
            var endTSMatch = Regex.Match(endTS.OriginalComment, @"(?i)mp ?(\d+)");

            if (startTSMatch.Success && endTSMatch.Success)
            {
                var startMp = double.Parse(startTSMatch.Groups[1].Value);
                var endMp = double.Parse(endTSMatch.Groups[1].Value);
                if (endMp < startMp)
                    allSolution = allSolution.Reverse();
            }
            else if (endTSMatch.Success)
            {
                if (double.Parse(endTSMatch.Groups[1].Value) == 0)
                    allSolution = allSolution.Reverse();
            }
            else if(files.Count == 1 && first.Name.ToLower().Contains("rev"))
                allSolution = allSolution.Reverse();

            allSolution.CalculateOffset(10);
            var solString = allSolution.ToString();
            var combined = new CombinedAllegroCISFile(name, type, allSolution);
            calc.Dispose();
            return combined;
        }



        public struct FileInfo
        {
            public int Start;
            public double StartFootage => File.Points[Start].Footage;
            public int End;
            public double EndFootage => File.Points[End].Footage;
            public double TotalFootage => Math.Abs(StartFootage - EndFootage);
            public int TotalPoints => Math.Abs(Start - End);
            public double Offset;
            public AllegroCISFile File;

            public FileInfo(AllegroCISFile file)
            {
                File = file;
                Offset = 0;
                Start = 0;
                End = file.Points.Count;
            }

            public FileInfo(AllegroCISFile file, int first, int last, double offset = 0)
            {
                File = file;
                Offset = offset;
                Start = first;
                End = last;
            }

            public override string ToString()
            {
                var numPoints = Math.Abs(End - Start) + 1;
                var isReversed = Start > End;
                var numFoot = Math.Abs(File.Points[Start].Footage - File.Points[End].Footage);
                var output = $"'{File.Name}'{(isReversed ? " Rev Run" : "")} offset {Offset} feet {numPoints} reads of {File.Points.Count} from {Start}|{File.Points[Start].Footage} to {End}|{File.Points[End].Footage} over {numFoot} feet.";
                return output;
            }
        }

        public class FileInfoLinkedList
        {
            public FileInfoLinkedList Prev;
            public FileInfoLinkedList Next;
            public FileInfo Info;
            public FileInfoLinkedList First => Prev == null ? this : Prev.First;
            public FileInfoLinkedList Last => Next == null ? this : Next.Last;
            public int TotalPoints => Info.TotalPoints + Next?.TotalPoints ?? 0;
            public double TotalFootage => Info.TotalFootage + Info.Offset + Next?.TotalFootage ?? 0;

            public override string ToString()
            {
                return Info.ToString() + "\n" + Next?.ToString() ?? "";
            }

            public FileInfoLinkedList(FileInfo info)
            {
                Info = info;
            }

            public FileInfoLinkedList AddToEnd(FileInfo info)
            {
                var temp = Last;
                Last.Next = new FileInfoLinkedList(info);
                Last.Prev = temp;
                return Last;
            }

            public FileInfoLinkedList AddToBeginning(FileInfo info)
            {
                First.Prev = new FileInfoLinkedList(info);
                return First;
            }

            public FileInfoLinkedList AddNext(FileInfo info)
            {
                var temp = Next;
                Next = new FileInfoLinkedList(info)
                {
                    Prev = this,
                    Next = temp
                };
                if (temp != null)
                    temp.Prev = Next;

                return Next;
            }

            public FileInfoLinkedList AddPrev(FileInfo info)
            {
                var temp = Next;
                Next = new FileInfoLinkedList(info)
                {
                    Prev = this,
                    Next = temp
                };
                if (temp != null)
                    temp.Prev = Next;

                return Next;
            }

            public void CalculateOffset(double roundTo)
            {
                if (Prev != null)
                {
                    var info = Prev.Info;
                    var otherFile = info.File;
                    var otherEnd = info.End;
                    var myFile = Info.File;
                    var myStart = Info.Start;
                    Info.Offset = Math.Max(myFile.OffsetDistance(myStart, otherFile, otherEnd, roundTo), roundTo);
                }
                if (Next != null)
                    Next.CalculateOffset(roundTo);
            }

            public FileInfoLinkedList Reverse()
            {
                var tempPrev = Prev;
                var tempNext = Next;
                var tempStart = Info.Start;
                var tempEnd = Info.End;
                Info.Start = tempEnd;
                Info.End = tempStart;
                Prev = tempNext;
                Next = tempPrev;
                if (Prev == null)
                    return this;
                return Prev.Reverse();
            }
        }

        private class OrderCalculator : IDisposable
        {
            List<(AllegroCISFile File, List<int> Indicies)> Files = new List<(AllegroCISFile File, List<int> Indicies)>();
            Dictionary<string, double> MinimumValues = new Dictionary<string, double>();
            Dictionary<string, double> MaxFootages = new Dictionary<string, double>();
            Dictionary<string, List<(int Index, int Start, int End)>> Solutions = new Dictionary<string, List<(int Index, int Start, int End)>>();
            string BaseUsings;
            string AllUsed;
            ulong NumberOfChecks = 0;
            ulong NumberOFTestStations;
            ulong MaxChecks = 1;

            public OrderCalculator(List<AllegroCISFile> files)
            {
                ulong numTestStations = 0;
                foreach (var file in files)
                {
                    var testStations = new List<int>();
                    testStations.Add(0);
                    var lastFoot = file.Points[0].Footage;
                    for (int i = 1; i < file.Points.Count - 1; ++i)
                    {
                        var curPoint = file.Points[i];
                        AllegroDataPoint nextPoint = file.Points[i + 1];
                        if (curPoint.TestStationReads.Count != 0 && !file.Name.ToLower().Contains("redo"))
                        {
                            if (i == 1 && curPoint.Footage - lastFoot <= 10)
                                continue;

                            if (nextPoint.TestStationReads.Count != 0)
                            {
                                if (nextPoint.Footage - curPoint.Footage <= 10)
                                    continue;
                            }
                            testStations.Add(i);
                        }
                        else if (nextPoint.Footage - curPoint.Footage > (5 * 10))
                        {
                            testStations.Add(i);
                        }
                        lastFoot = curPoint.Footage;
                    }
                    var lastIndex = file.Points.Count - 1;
                    //191B
                    //if (file.Points[lastIndex].Footage - lastFoot <= 10)
                    testStations.Add(lastIndex);
                    numTestStations += (ulong)testStations.Count;
                    Files.Add((file, testStations));
                }
                NumberOFTestStations = numTestStations;
                BaseUsings = "".PadLeft(files.Count, '0');
                AllUsed = "".PadLeft(files.Count, '1');
                MinimumValues.Add(AllUsed, double.MaxValue);
                MaxFootages.Add(AllUsed, double.MinValue);
            }

            public void AsyncSolve()
            {
                if(BaseUsings.Length == 1)
                {
                    var end = Files.First().File.Points.Count - 1;
                    Solutions.Add(AllUsed, new List<(int Index, int Start, int End)>() { (0, 0, end)});
                    return;
                }
                var tasks = new List<Task>();
                for (int i = 0; i < BaseUsings.Length; ++i)
                {
                    var curRequired = i;
                    var task = Task.Run(() => Solve(required: curRequired));
                    tasks.Add(task);
                }
                Task.WaitAll(tasks.ToArray());
            }

            public void Solve(string currentActive = null, List<(int Index, int Start, int End)> values = null, (int Index, int Start, int End)? newValue = null, double curOffset = 0, double footCovered = 0, double roundTo = 10, int? required = null)
            {
                ++NumberOfChecks;
                if (currentActive == null)
                    currentActive = BaseUsings;
                if (values == null)
                    values = new List<(int Index, int Start, int End)>();
                else
                    values = new List<(int Index, int Start, int End)>(values);
                lock (MinimumValues)
                {
                    if (!MinimumValues.ContainsKey(currentActive))
                    {
                        MinimumValues.Add(currentActive, double.MaxValue);
                        MaxFootages.Add(currentActive, double.MinValue);
                        Solutions.Add(currentActive, null);
                    }
                    if (newValue.HasValue)
                        values.Add(newValue.Value);
                    var curMin = MinimumValues[currentActive];
                    var curMaxFoot = MaxFootages[currentActive];
                    if (curOffset > curMin || curOffset > MinimumValues[AllUsed])
                        return;

                    if (curOffset == curMin)
                    {
                        if (values.Count < Solutions[currentActive].Count)
                        {
                            MinimumValues[currentActive] = curOffset;
                            Solutions[currentActive] = values;
                            MaxFootages[currentActive] = footCovered;
                        }
                    }
                    else if (curOffset < curMin)
                    {
                        MinimumValues[currentActive] = curOffset;
                        Solutions[currentActive] = values;
                    }
                    if (currentActive.Equals(AllUsed))
                    {
                        return;
                    }
                }
                var (Index, _, End) = values.Count > 0 ? values.Last() : (-1, 0, 0);
                var lastIndicies = Index != -1 ? Files[Index].Indicies : null;
                var lastFile = Index != -1 ? Files[Index].File : null;
                var indexStart = required ?? 0;
                var indexLength = required ?? (currentActive.Length - 1);
                for (int i = indexStart; i <= indexLength; ++i)
                {
                    if (currentActive[i] == '1')
                        continue;
                    var file = Files[i];
                    for (int s = 0; s < file.Indicies.Count; ++s)
                    {
                        var start = file.Indicies[s];
                        for (int e = file.Indicies.Count - 1; e >= 0; --e)
                        {
                            var end = file.Indicies[e];
                            if (start == end)
                                continue;
                            var newFootage = Math.Abs(file.File.Points[start].Footage - file.File.Points[end].Footage);
                            var newOffset = lastFile?.OffsetDistance(End, file.File, start, roundTo) ?? 0;
                            if (newOffset > (2 * 10))
                            {
                                if (start != file.Indicies[0] && start != file.Indicies[file.Indicies.Count - 1])
                                    continue;
                                if (end != lastIndicies[0] && end != lastIndicies[lastIndicies.Count - 1])
                                    continue;
                            }
                            lock (MinimumValues)
                            {
                                if (curOffset + newOffset > MinimumValues[AllUsed])
                                    continue;
                                var newActive = ActivateUsing(currentActive, i);
                                Solve(newActive, values, (i, start, end), curOffset + newOffset, footCovered + newFootage, roundTo);
                                if (curOffset > MinimumValues[AllUsed])
                                    return;
                            }
                        }
                    }
                }
                for (int i = indexStart; i <= indexLength; ++i)
                {
                    if (currentActive[i] == '0' || i == Index)
                        continue;
                    var file = Files[i];
                    var availablePairs = GetStartAndEndPairs(values, i);
                    foreach (var (start, end) in availablePairs)
                    {
                        var newOffset = lastFile?.OffsetDistance(End, file.File, start, roundTo) ?? 0;
                        if (newOffset > (2 * 10))
                        {
                            if (start != file.Indicies[0] && start != file.Indicies[file.Indicies.Count - 1])
                                continue;
                            if (end != lastIndicies[0] && end != lastIndicies[lastIndicies.Count - 1])
                                continue;
                        }
                        if (curOffset + newOffset > MinimumValues[AllUsed])
                            continue;
                        lock (MinimumValues)
                        {
                            var newActive = ActivateUsing(currentActive, i);
                            var newFootage = Math.Abs(file.File.Points[start].Footage - file.File.Points[end].Footage);
                            Solve(newActive, values, (i, start, end), curOffset + newOffset, footCovered + newFootage, roundTo);
                            if (curOffset > MinimumValues[AllUsed])
                                return;
                        }
                    }
                }
            }

            private List<(int Start, int End)> GetStartAndEndPairs(List<(int Index, int Start, int End)> values, int index)
            {
                var availablePairs = new List<(int Start, int End)>();
                var usedPoints = new List<(int Start, int End)>();
                foreach (var (curIndex, start, end) in values)
                {
                    if (curIndex == index)
                        usedPoints.Add((start, end));
                }
                var file = Files[index];
                for (int s = 0; s < file.Indicies.Count; ++s)
                {
                    var start = file.Indicies[s];
                    for (int e = file.Indicies.Count - 1; e >= 0; --e)
                    {
                        var end = file.Indicies[e];
                        if (start == end)
                            continue;
                        var available = true;
                        foreach (var (usedStart, usedEnd) in usedPoints)
                        {
                            var minUsed = Math.Min(usedStart, usedEnd);
                            var maxUsed = Math.Max(usedStart, usedEnd);
                            if (start >= minUsed && start <= maxUsed)
                            {
                                available = false;
                                break;
                            }
                            if (end >= minUsed && end <= maxUsed)
                            {
                                available = false;
                                break;
                            }
                            if (start < minUsed != end < minUsed)
                            {
                                available = false;
                                break;
                            }
                            if (start > maxUsed != end > maxUsed)
                            {
                                available = false;
                                break;
                            }
                        }
                        if (available)
                            availablePairs.Add((start, end));
                    }
                }

                return availablePairs;
            }

            public FileInfoLinkedList GetAllUsedSolution()
            {
                if (Solutions.Count == 0)
                    return null;
                var list = Solutions[AllUsed];

                var (Index, Start, End) = list[0];
                var firstFile = Files[Index].File;
                var firstIndicies = Files[Index].Indicies;
                FileInfo firstInfo;
                if (Start < End)
                    firstInfo = new FileInfo(firstFile, firstIndicies[0], End);
                else
                    firstInfo = new FileInfo(firstFile, firstIndicies[firstIndicies.Count - 1], End);
                FileInfoLinkedList output = new FileInfoLinkedList(firstInfo);
                if (list.Count == 1)
                    return output;
                for (int i = 1; i < list.Count - 1; ++i)
                {
                    var cur = list[i];
                    var info = new FileInfo(Files[cur.Index].File, cur.Start, cur.End);
                    output.AddToEnd(info);
                }

                (Index, Start, End) = list[list.Count - 1];
                var lastFile = Files[Index].File;
                var lastIndicies = Files[Index].Indicies;
                FileInfo lastInfo;
                if (End < Start)
                    lastInfo = new FileInfo(lastFile, Start, lastIndicies[0]);
                else
                    lastInfo = new FileInfo(lastFile, Start, lastIndicies[lastIndicies.Count - 1]);
                output.AddToEnd(lastInfo);

                return output;
            }

            private string ActivateUsing(string current, int index)
            {
                if (index == current.Length - 1)
                    return current.Substring(0, index) + "1";
                if (index == 0)
                    return "1" + current.Substring(1);
                return current.Substring(0, index) + "1" + current.Substring(index + 1);
            }

            public void Dispose()
            {
                Files = null;
                MinimumValues = null;
                Solutions = null;
            }
        }
    }
}
