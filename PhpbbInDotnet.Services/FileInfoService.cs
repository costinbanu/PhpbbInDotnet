using System;
using System.IO;

namespace PhpbbInDotnet.Services
{
    class FileInfoService : IFileInfoService
    {
        public DateTime? GetLastWriteTime(string fileName)
        {
            DateTime? lastRun = null;
            try
            {
                lastRun = new FileInfo(fileName).LastWriteTimeUtc;
            }
            catch { }

            return lastRun;
        }
    }
}
