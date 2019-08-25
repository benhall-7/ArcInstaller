using System;

namespace CrossInstaller.System
{
    public class CompressEventArgs : EventArgs
    {
        /// <summary>
        /// The compression level (from 1-22) of the algorithm attempt
        /// </summary>
        public int CompressionLevel { get; }

        public CompressEventArgs(int compressionLevel)
        {
            CompressionLevel = compressionLevel;
        }
    }
}
