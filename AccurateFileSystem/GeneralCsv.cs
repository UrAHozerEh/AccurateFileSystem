using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateFileSystem
{
    public class GeneralCsv : File
    {
        public List<string> Headers;
        public string[,] Data { get; set; }

        private string CurItem;
        private List<string> Output;
        private bool InString;
        private bool InGps;

        public GeneralCsv(string name, List<string> lines) : base(name, FileType.Unknown)
        {
            ParseLines(lines);
        }

        protected GeneralCsv(string name, List<string> lines, FileType type) : base(name, type)
        {
            ParseLines(lines);
        }

        public int GetColumn(string headerName)
        {
            for(int i = 0; i < Headers.Count; ++i)
            {
                if (Headers[i] == headerName)
                    return i;
            }
            return -1;
        }

        private void ParseLines(List<string> lines)
        {
            var firstLine = ParseLine(lines, 0, out var nextIndex);
            Headers = new List<string>();
            for (int i = 0; i < firstLine.Count; ++i)
            {
                var text = firstLine[i];
                Headers.Add(text);
            }


            var dataRows = new List<List<string>>();
            while (nextIndex < lines.Count)
            {
                var lineData = ParseLine(lines, nextIndex, out nextIndex);
                if (lineData.Count > Headers.Count)
                    continue;
                dataRows.Add(lineData);

            }
            Data = new string[dataRows.Count, Headers.Count];
            for (int r = 0; r < dataRows.Count; ++r)
            {
                var row = dataRows[r];
                for (int c = 0; c < row.Count; ++c)
                {
                    Data[r, c] = row[c];
                }
            }
        }

        private List<string> ParseLine(List<string> lines, int index, out int nextIndex)
        {
            var line = lines[index];
            Output = new List<string>();

            CurItem = "";
            InString = false;
            InGps = false;
            nextIndex = index + 1;
            for (int i = 0; i < line.Length; ++i)
            {
                var curChar = line[i];
                var nextI = i + 1;
                var nextIsQuote = (nextI < line.Length) && (line[nextI] == '"');
                ParseCharacter(curChar, nextIsQuote, out bool skipNext);
                if (i == line.Length - 1 && InString)
                {
                    line += " " + lines[nextIndex];
                    ++nextIndex;
                }
                if (skipNext)
                    ++i;
            }
            Output.Add(CurItem);

            return Output;
        }

        private void ParseCharacter(char curChar, bool nextIsQuote, out bool skipNext)
        {
            skipNext = false;
            switch (curChar)
            {
                case '°':
                    //if (!InString)
                    //    InGps = true;
                    break;
                case ',':
                    if (InString)
                    {
                        CurItem += curChar;
                    }
                    else
                    {
                        Output.Add(CurItem);
                        CurItem = "";
                    }
                    break;
                case '"':
                    if (nextIsQuote)
                    {
                        CurItem += curChar;
                        skipNext = true;
                    }
                    else if (CurItem == "")
                    {
                        InString = true;
                    }
                    else if (InGps)
                    {
                        InGps = false;
                        CurItem += curChar;
                    }
                    else if (InString)
                    {
                        InString = false;
                    }
                    else
                    {
                        CurItem += curChar;
                    }
                    break;
                default:
                    CurItem += curChar;
                    break;
            }
        }

        public override bool IsEquivalent(File otherFile)
        {
            if (!(otherFile is GeneralCsv))
                return false;
            var otherCsv = otherFile as GeneralCsv;
            return Name == otherCsv.Name;
        }
    }
}
