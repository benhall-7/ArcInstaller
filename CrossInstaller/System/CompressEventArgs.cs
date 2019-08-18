using System;

namespace CrossInstaller.System
{
    public class CompressEventArgs : EventArgs
    {
        public int CompressionLevel { get; }

        public CompressEventArgs(int compressionLevel)
        {
            CompressionLevel = compressionLevel;
        }
    }
}
