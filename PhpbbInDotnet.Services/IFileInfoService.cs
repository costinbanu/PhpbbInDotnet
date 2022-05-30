using System;

namespace PhpbbInDotnet.Services
{
    public interface IFileInfoService
    {
        DateTime? GetLastWriteTime(string fileName);
    }
}