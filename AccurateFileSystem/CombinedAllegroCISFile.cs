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

            files.Remove(first);



            var combined = new CombinedAllegroCISFile(name, type, header, fileInfos);
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
            public int First;
            public int Last;
            public double Offset;
            public AllegroCISFile File;

            public FileInfo(AllegroCISFile file)
            {
                File = file;
                Offset = 0;
                First = 0;
                Last = file.Points.Count;
            }

            public FileInfo(AllegroCISFile file, int first, int last, double offset = 0)
            {
                File = file;
                Offset = offset;
                First = first;
                Last = last;
            }
        }

        public class FileInfoLinkedList
        {
            public FileInfoLinkedList Prev;
            public FileInfoLinkedList Next;
            public FileInfo Info;
            public FileInfoLinkedList First => Prev == null ? this : Prev.First;
            public FileInfoLinkedList Last => Next == null ? this : Next.First;

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

            public void CalculateOffset(double closestNumber)
            {
                if (Prev != null)
                {
                    var prevInfo = Prev.Info;
                    var prevFile = prevInfo.File;
                    var prevCompareIndex = prevFile.IsReverseRun ? prevInfo.First : prevInfo.Last;
                    var maxIndex = !prevFile.IsReverseRun ? prevInfo.First : prevInfo.Last;
                    BasicGeoposition? prevGps = null;
                    var change = prevFile.IsReverseRun ? 1 : -1;
                    while (prevCompareIndex != maxIndex + change && !prevFile.Points[prevCompareIndex].HasGPS)
                        prevCompareIndex += change;
                    if (prevCompareIndex == maxIndex + change || !prevFile.Points[prevCompareIndex].HasGPS)
                        throw new InvalidOperationException("There is no GPS to calculate offset with!");
                    var prevGPS = Info.File.Points[prevCompareIndex].GPS;

                    var myFile = Info.File;
                    var myCompareIndex = myFile.IsReverseRun ? Info.First : Info.Last;
                    var myMaxIndex = !myFile.IsReverseRun ? Info.First : Info.Last;
                    BasicGeoposition? myGps = null;
                    var myChange = myFile.IsReverseRun ? 1 : -1;
                    while (myCompareIndex != myMaxIndex + myChange && !myFile.Points[myCompareIndex].HasGPS)
                        myCompareIndex += myChange;
                    if (myCompareIndex == myMaxIndex + myChange || !myFile.Points[myCompareIndex].HasGPS)
                        throw new InvalidOperationException("There is no GPS to calculate offset with!");
                    var myGPS = Info.File.Points[myCompareIndex].GPS;

                    var dist = myGPS.Distance(prevGPS);
                    int mult = (int)(dist / closestNumber);
                    var trueDist = mult * closestNumber;
                    Info.Offset = trueDist;
                }
                if (Next != null)
                    Next.CalculateOffset(closestNumber);
            }
        }
    }
}
