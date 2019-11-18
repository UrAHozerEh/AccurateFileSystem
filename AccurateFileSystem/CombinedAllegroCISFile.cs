using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace AccurateFileSystem
{
    public class CombinedAllegroCISFile
    {
        public FileInfoLinkedList FileInfos { get; set; }
        public FileType Type { get; set; }
        public string Name { get; set; }
        public List<(double Footage, bool IsReverse, AllegroDataPoint Point)> Points = null;

        private CombinedAllegroCISFile(string name, FileType type, FileInfoLinkedList fileInfos)
        {
            Type = type;
            Name = name;
            FileInfos = fileInfos;
            UpdatePoints();
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
            var list = new List<(double Footage, bool IsReverse, AllegroDataPoint Point)>();
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
                    list.Add((footage, isReverse, curPoint));
                }
                offset += info.TotalFootage;
                tempFileInfoNode = tempFileInfoNode.Next;
            }
            Points = list;
        }

        public List<(double Footage, double On, double Off)> GetCombinedMirData()
        {
            var list = new List<(double, double, double)>();
            for (int i = 0; i < Points.Count; ++i)
            {
                list.Add((Points[i].Footage, Points[i].Point.MirOn, Points[i].Point.MirOff));
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
                list.Add((Points[i].Footage, Points[i].Point.MirOn));
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
                list.Add((Points[i].Footage, Points[i].Point.MirOff));
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
            calc.Solve();
            //TODO: Maybe look at TS MP to determine if we should reverse the new file.
            var allSolution = calc.GetAllUsedSolution().Reverse();
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
                    if (file.Points[0].TestStationReads.Count == 0 || file.Name.ToLower().Contains("redo"))
                        testStations.Add(0);
                    for (int i = 0; i < file.Points.Count; ++i)
                    {
                        if (file.Points[i].TestStationReads.Count != 0 && !file.Name.ToLower().Contains("redo"))
                            testStations.Add(i);
                    }
                    var lastIndex = file.Points.Count - 1;
                    if (file.Points[lastIndex].TestStationReads.Count == 0)
                        testStations.Add(lastIndex);
                    numTestStations += (ulong)testStations.Count;
                    Files.Add((file, testStations));
                }
                NumberOFTestStations = numTestStations;
                while (numTestStations > 1)
                {
                    try
                    {
                        MaxChecks *= numTestStations;
                        --numTestStations;
                    }
                    catch
                    {
                        MaxChecks = ulong.MaxValue;
                        break;
                    }
                }
                BaseUsings = "".PadLeft(files.Count, '0');
                AllUsed = "".PadLeft(files.Count, '1');
                MinimumValues.Add(AllUsed, double.MaxValue);
                MaxFootages.Add(AllUsed, double.MinValue);
            }

            public void Solve(string currentActive = null, List<(int Index, int Start, int End)> values = null, (int Index, int Start, int End)? newValue = null, double curOffset = 0, double footCovered = 0, double roundTo = 10)
            {
                ++NumberOfChecks;
                if (currentActive == null)
                    currentActive = BaseUsings;
                if (values == null)
                    values = new List<(int Index, int Start, int End)>();
                else
                    values = new List<(int Index, int Start, int End)>(values);
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

                var (Index, _, End) = values.Count > 0 ? values.Last() : (-1, 0, 0);
                var lastIndicies = Index != -1 ? Files[Index].Indicies : null;
                var lastFile = Index != -1 ? Files[Index].File : null;
                for (int i = 0; i < currentActive.Length; ++i)
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
                            if (curOffset + newOffset > MinimumValues[AllUsed])
                                continue;
                            var newActive = ActivateUsing(currentActive, i);
                            Solve(newActive, values, (i, start, end), curOffset + newOffset, footCovered + newFootage, roundTo);
                            if (curOffset > MinimumValues[AllUsed])
                                return;
                        }
                    }
                }
                for (int i = 0; i < currentActive.Length; ++i)
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
                        var newActive = ActivateUsing(currentActive, i);
                        var newFootage = Math.Abs(file.File.Points[start].Footage - file.File.Points[end].Footage);
                        Solve(newActive, values, (i, start, end), curOffset + newOffset, footCovered + newFootage, roundTo);
                        if (curOffset > MinimumValues[AllUsed])
                            return;
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
