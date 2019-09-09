using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateFileSystem
{
    public abstract class OnOffFile : File
    {
        public override FileType Type => FileType.OnOff;
    }
}
