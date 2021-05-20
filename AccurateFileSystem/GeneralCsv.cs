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
        public string[,] Data;

        private string CurItem;
        private List<string> Output;
        private bool InString;

        public GeneralCsv(string name, List<string> lines) : base(name, FileType.Unknown)
        {
            ParseLines(lines);
        }

        protected GeneralCsv(string name, List<string> lines, FileType type) : base(name, type)
        {
            ParseLines(lines);
        }

        private void ParseLines(List<string> lines)
        {
            var firstLine = ParseLine(lines[0]);
            Headers = new List<string>();
            for (int i = 0; i < firstLine.Count; ++i)
            {
                var text = firstLine[i];
                Headers.Add(text);
            }

            Data = new string[lines.Count - 1, Headers.Count];
            for (int r = 1; r < lines.Count; ++r)
            {
                var lineData = ParseLine(lines[r]);
                if (lineData.Count > Headers.Count)
                    continue;
                for (int c = 0; c < lineData.Count; ++c)
                {
                    Data[r - 1, c] = lineData[c];
                }
            }
        }

        private List<string> ParseLine(string line)
        {
            Output = new List<string>();

            CurItem = "";
            InString = false;
            for (int i = 0; i < line.Length; ++i)
            {
                var curChar = line[i];
                var nextI = i + 1;
                var nextIsQuote = (nextI < line.Length) && (line[nextI] == '"');
                ParseCharacter(curChar, nextIsQuote, out bool skipNext);
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
                    else if(CurItem == "")
                    {
                        InString = true;
                    }
                    else if(InString)
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
