using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace AccurateFileSystem
{
    public class CombinedAllegroCISFile : AllegroCISFile
    {
        public List<FileInfo> FileInfos { get; set; }
        private CombinedAllegroCISFile(string name, FileType type, Dictionary<string, string> header, List<FileInfo> fileInfos) : base(name, ".svy", header, new Dictionary<int, AllegroDataPoint>(), type)
        {
            FileInfos = fileInfos;
            foreach (var fileInfo in FileInfos)
            {

            }
        }

        public static CombinedAllegroCISFile CombineFiles(string name, List<AllegroCISFile> files)
        {
            var first = files.First();
            var header = first.Header;
            var type = first.Type;

            var calc = new OrderCalculator(files);
            calc.Solve();

            var combined = new CombinedAllegroCISFile(name, type, header, null);
            return combined;
        }

        public static CombinedAllegroCISFile CombineFiles(string name, List<FileInfo> fileInfos)
        {
            var first = fileInfos.First().File;
            var header = first.Header;
            var type = first.Type;
            var combined = new CombinedAllegroCISFile(name, type, header, fileInfos);
            return combined;
        }

        public static CombinedAllegroCISFile CombineOrderedFiles(string name, List<FileInfo> fileInfos)
        {
            var first = fileInfos.First().File;
            var header = first.Header;
            var type = first.Type;
            var combined = new CombinedAllegroCISFile(name, type, header, fileInfos);
            return combined;
        }



        public struct FileInfo
        {
            public int Start;
            public int End;
            public double Offset;
            public AllegroCISFile File;
            public bool IsReversed;

            public FileInfo(AllegroCISFile file)
            {
                File = file;
                Offset = 0;
                Start = 0;
                End = file.Points.Count;
                IsReversed = false;
            }

            public FileInfo(AllegroCISFile file, int first, int last, bool isReversed, double offset = 0)
            {
                File = file;
                Offset = offset;
                Start = first;
                End = last;
                IsReversed = isReversed;
            }
        }

        public class FileInfoLinkedList
        {
            public FileInfoLinkedList Prev;
            public FileInfoLinkedList Next;
            public FileInfo Info;
            public FileInfoLinkedList First => Prev == null ? this : Prev.First;
            public FileInfoLinkedList Last => Next == null ? this : Next.First;
            public double Offset;

            public FileInfoLinkedList(FileInfo info)
            {
                Info = info;
            }

            public FileInfoLinkedList AddToEnd(FileInfo info)
            {
                Last.Next = new FileInfoLinkedList(info);
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
                    Offset = myFile.OffsetDistance(myStart, otherFile, otherEnd, roundTo);
                }
                if (Next != null)
                    Next.CalculateOffset(roundTo);
            }
        }

        private class OrderCalculator
        {
            List<(AllegroCISFile File, List<int> Indicies)> Files = new List<(AllegroCISFile File, List<int> Indicies)>();
            Dictionary<string, double> MinimumValues = new Dictionary<string, double>();
            Dictionary<string, List<(int Index, int Start, int End)>> Solutions = new Dictionary<string, List<(int Index, int Start, int End)>>();
            string BaseUsings;
            string AllUsed;

            public OrderCalculator(List<AllegroCISFile> files)
            {
                foreach (var file in files)
                {
                    var testStations = new List<int>();
                    if (file.Points[0].TestStationReads.Count == 0)
                        testStations.Add(0);
                    for (int i = 0; i < file.Points.Count; ++i)
                    {
                        if (file.Points[i].TestStationReads.Count != 0)
                            testStations.Add(i);
                    }
                    var lastIndex = file.Points.Count - 1;
                    if (file.Points[lastIndex].TestStationReads.Count == 0)
                        testStations.Add(lastIndex);

                    Files.Add((file, testStations));
                }
                BaseUsings = "".PadLeft(files.Count, '0');
                AllUsed = "".PadLeft(files.Count, '1');
                MinimumValues.Add(AllUsed, double.MaxValue);
            }

            public void Solve(string currentActive = null, List<(int Index, int Start, int End)> values = null, (int Index, int Start, int End)? newValue = null, double curOffset = 0, double roundTo = 10)
            {
                if (currentActive == null)
                    currentActive = BaseUsings;
                if (values == null)
                    values = new List<(int Index, int Start, int End)>();
                if (!MinimumValues.ContainsKey(currentActive))
                {
                    MinimumValues.Add(currentActive, double.MaxValue);
                    Solutions.Add(currentActive, null);
                }
                if (newValue.HasValue)
                    values.Add(newValue.Value);
                var curMin = MinimumValues[currentActive];
                if (curOffset > curMin || curOffset > MinimumValues[AllUsed])
                    return;
                if (curOffset < curMin)
                {
                    MinimumValues[currentActive] = curOffset;
                    Solutions[currentActive] = values;
                }
                if (currentActive.Equals(AllUsed))
                    return;
                var (Index, _, End) = values.Count > 0 ? values.Last() : (-1, 0, 0);
                var lastFile = Index != -1 ? Files[Index].File : null;
                for (int i = 0; i < currentActive.Length; ++i)
                {
                    if (currentActive[i] == '1')
                        continue;
                    foreach (var file in Files)
                    {
                        foreach (var start in file.Indicies)
                        {
                            foreach (var end in file.Indicies)
                            {
                                if (start == end)
                                    continue;
                                //TODO: Breaking here.
                                var newOffset = lastFile?.OffsetDistance(End, file.File, start, roundTo) ?? 0;
                                if (curOffset + newOffset > MinimumValues[AllUsed])
                                    continue;
                                var newActive = ActivateUsing(currentActive, i);
                                Solve(newActive, values, (i, start, end), curOffset + newOffset, roundTo);
                                if (curOffset > MinimumValues[AllUsed])
                                    return;
                            }
                        }
                    }
                }
            }

            private string ActivateUsing(string current, int index)
            {
                if (index == current.Length - 1)
                    return current.Substring(0, index) + "1";
                if (index == 0)
                    return "1" + current.Substring(1);
                return current.Substring(0, index) + "1" + current.Substring(index + 1);

            }
        }
    }
}
