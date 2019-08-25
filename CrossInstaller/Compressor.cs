using CrossInstaller.System;
using System;
using System.IO;
using Zstandard.Net;

namespace CrossInstaller
{
    public class Compressor
    {
        private static readonly byte[] padData = { 2, 0, 0, 0 };

        private int startLevel = 22;
        private int endLevel = 1;

        /// <summary>
        /// The event invoked when the compression algorithm starts
        /// </summary>
        public event EventHandler StartCompressEvent;

        /// <summary>
        /// The event invoked each time an attempt at using a compression level is made
        /// </summary>
        public event EventHandler<CompressEventArgs> TryCompressEvent;

        /// <summary>
        /// The event invoked when the compression algorithm succeeds
        /// </summary>
        public event EventHandler<CompressEventArgs> SucceedCompressEvent;

        /// <summary>
        /// The starting compression level used in the compression algorithm.
        /// Default value is 22
        /// </summary>
        public int CompressionLevelStart
        {
            get => startLevel;
            set
            {
                if (value < 1 || value > 22)
                    throw new Exception("Compression level must be between 1 and 22 inclusive");
                startLevel = value;
            }
        }
        /// <summary>
        /// The ending compression level used in the compression algorithm.
        /// Default value is 1
        /// </summary>
        public int CompressionLevelEnd
        {
            get => endLevel;
            set
            {
                if (value < 1 || value > 22)
                    throw new Exception("Compression level must be between 1 and 22 inclusive");
                endLevel = value;
            }
        }

        public byte[] Compress(string file, uint compSize, uint decompSize)
        {
            if (compSize == decompSize)
            {
                return File.ReadAllBytes(file);
            }

            StartCompressEvent?.Invoke(this, new EventArgs());

            byte[] inputFile = File.ReadAllBytes(file);

            byte[] compFile;

            if (startLevel < endLevel)
            {
                for (int i = startLevel; i <= endLevel; i++)
                {
                    if (TryCompPad(inputFile, compSize, i, out compFile))
                        return compFile;
                }
            }
            else
            {
                for (int i = startLevel; i >= endLevel; i--)
                {
                    if (TryCompPad(inputFile, compSize, i, out compFile))
                        return compFile;
                }
            }

            throw new CompressFailException("File unable to be compressed to the correct size");
        }

        private bool TryCompPad(byte[] file, uint compSize, int level, out byte[] comp)
        {
            TryCompressEvent?.Invoke(this, new CompressEventArgs(level));
            using (var memStream = new MemoryStream())
            using (var zstdStream = new ZstandardStream(memStream, level))
            {
                zstdStream.Write(file, 0, file.Length);
                zstdStream.Close();
                comp = memStream.ToArray();
            }

            var padSize = compSize - comp.Length;
            if (padSize < 0 || padSize == 1 || padSize == 2 || padSize == 5)
                return false;

            //padding mechanism by Birdwards: https://github.com/Birdwards/SmashPad/blob/master/smashpad.py
            byte Frame_Header_Descriptor = comp[4];

            int start_index = 6;
            if (Frame_Header_Descriptor >= 0xc0)
                start_index = 13;
            else if (Frame_Header_Descriptor >= 0x80)
                start_index = 9;
            else if (Frame_Header_Descriptor >= 0x40)
                start_index = 7;

            if (start_index > 6 && (Frame_Header_Descriptor & 0x3f) < 0x20)
                start_index += 1;

            if ((Frame_Header_Descriptor & 0x3) == 1)
                start_index += 1;
            else if ((Frame_Header_Descriptor & 0x3) == 2)
                start_index += 2;
            else if ((Frame_Header_Descriptor & 0x3) == 3)
                start_index += 4;

            using (var compWithPadStream = new MemoryStream())
            {
                compWithPadStream.Write(comp, 0, start_index);

                byte[] padData = new byte[] { 2, 0, 0, 0 };
                if (padSize % 3 == 0)
                {
                    for (int i = 0; i < padSize; i++)
                        compWithPadStream.WriteByte(0);
                }
                else if (padSize % 3 == 1)
                {
                    for (int i = 0; i < padSize - 4; i++)
                        compWithPadStream.WriteByte(0);
                    compWithPadStream.Write(padData, 0, padData.Length);
                }
                else if (padSize % 3 == 2)
                {
                    for (int i = 0; i < padSize - 8; i++)
                        compWithPadStream.WriteByte(0);
                    compWithPadStream.Write(padData, 0, padData.Length);
                    compWithPadStream.Write(padData, 0, padData.Length);
                }

                compWithPadStream.Write(comp, start_index, comp.Length - start_index);

                if (compWithPadStream.Length != compSize)
                    throw new Exception("Error occurred in compression step, compression size mismatch");

                comp = compWithPadStream.ToArray();
            }

            SucceedCompressEvent?.Invoke(this, new CompressEventArgs(level));

            return true;
        }

        //public static byte[] Decompress()
        //{

        //}
    }
}
