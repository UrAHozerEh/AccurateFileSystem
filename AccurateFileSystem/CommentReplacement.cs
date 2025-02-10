using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace AccurateFileSystem
{
    public class CommentReplacements : File
    {
        public List<Replacement> Data { get; set; }
        public bool HasStartHcaReplacement => Data.Count(r => r.IsStart && r.IsHca) == 1;
        public string StartHcaReplacement => Data.First(r => r.IsStart && r.IsHca).Comment;
        public bool HasEndHcaReplacement => Data.Count(r => r.IsEnd && r.IsHca) == 1;
        public string EndHcaReplacement => Data.First(r => r.IsEnd && r.IsHca).Comment;
        public bool HasStartBufferReplacement => Data.Count(r => r.IsStart && r.IsBuffer) == 1;
        public string StartBufferReplacement => Data.First(r => r.IsStart && r.IsBuffer).Comment;
        public bool HasEndBufferReplacement => Data.Count(r => r.IsEnd && r.IsBuffer) == 1;
        public string EndBufferReplacement => Data.First(r => r.IsEnd && r.IsBuffer).Comment;


        public CommentReplacements(string name, List<Replacement> data) : base(name, FileType.CommentReplacements)
        {
            Data = data;
        }

        public static async Task<CommentReplacements> GetReplacements(StorageFile file)
        {
            var replacements = new List<Replacement>();
            var lines = await file.GetLines();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(',');
                if (parts.Length != 3) continue;
                var isStart = parts[0].Contains("y", StringComparison.InvariantCultureIgnoreCase);
                var isHca = parts[1].Contains("y", StringComparison.InvariantCultureIgnoreCase);
                var comment = parts[2];
                replacements.Add(new Replacement(isStart, isHca, comment));
            }
            return new CommentReplacements(file.DisplayName, replacements);
        }

        public override bool IsEquivalent(File otherFile)
        {
            throw new NotImplementedException();
        }

        public readonly struct Replacement
        {
            public bool IsStart { get; }
            public bool IsEnd => !IsStart;
            public bool IsHca { get; }
            public bool IsBuffer => !IsHca;
            public string Comment { get; }

            public Replacement(bool isStart, bool isHca, string comment)
            {
                IsStart = isStart;
                IsHca = isHca;
                Comment = comment;
            }

            public void Deconstruct(out bool isStart, out bool isHca, out string comment)
            {
                isStart = IsStart;
                isHca = IsHca;
                comment = Comment;
            }
        }
    }
}
