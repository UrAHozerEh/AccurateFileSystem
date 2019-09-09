using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace AccurateFileSystem.FileFactories
{
    public class AllegroCISFileFactory : FileFactory
    {
        private StorageFile StorageFile;

        public AllegroCISFileFactory(StorageFile storageFile)
        {
            StorageFile = storageFile;
        }

        public async override Task<File> GetFile()
        {

            using (var stream = await StorageFile.OpenStreamForReadAsync())
            using (var reader = new StreamReader(stream))
            {
                bool inHeader = true;
                string line = reader.ReadLine();
                if (line != "Start survey:") throw new Exception();
                var extension = StorageFile.FileType;
                while (!reader.EndOfStream)
                {
                    if (inHeader)
                    {

                    }
                    else
                    {

                    }
                }
            }
            throw new NotImplementedException();
        }
    }
}
