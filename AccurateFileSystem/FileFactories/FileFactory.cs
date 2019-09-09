using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateFileSystem.FileFactories
{
    public abstract class FileFactory
    {
        public abstract Task<File> GetFile();
    }
}
