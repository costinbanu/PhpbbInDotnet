using System;
using System.IO;

namespace PhpbbInDotnet.Objects;

public class ImageResizeResult(Stream stream, uint newWidth, uint newHeight) : IDisposable
{
    public Stream Stream { get; } = stream;
    public uint NewWidth { get; } = newWidth;
    public uint NewHeight { get; } = newHeight;

    public void Dispose()
    {
        Stream.Dispose();
        GC.SuppressFinalize(this);
    }
}