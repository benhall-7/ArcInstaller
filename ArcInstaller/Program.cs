using System;
using System.IO;
using ArcCross;
using Zstandard.Net;

namespace ArcInstaller
{
    class Program
    {
        static Mode Mode { get; set; }
        static string HelpText { get; set; } =
            "- ArcInstaller -\n" +
            "Usage: <mode> <options>\n" +
            "[Modes] = '-h' (print help)\n" +
            "        = '-i' (Inject)\n" +
            "        = '-e' (Extract)\n" +
            "Extract : <path to Arc>\n" +
            "          <path to name table>\n" +
            "          Optional: <extract folder>\n" +
            "Inject  : <path to Arc>\n" +
            "          <path to mod directory>\n" +
            "          <path to output Arc>\n" +
            "          ->  (if it does not exist, copies input arc)";

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("No input. See option '-h' for help");
                return;
            }
            switch (args[0])
            {
                case "-i":
                    Mode = Mode.Inject;
                    break;
                case "-e":
                    Mode = Mode.Extract;
                    break;
                case "-h":
                    Console.WriteLine(HelpText);
                    return;
                default:
                    Console.WriteLine($"Invalid option '{args[0]}'. See option '-h' for help");
                    return;
            }
            switch (Mode)
            {
                case Mode.Inject:
                    Inject(args);
                    break;
                case Mode.Extract:
                    Extract(args);
                    break;
            }
        }

        static void Extract(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Insufficient args. See -h for help");
                return;
            }
            string arcPath = args[1];
            string pathFile = args[2];
            string extractPath = "extract";
            if (args.Length > 3)
                extractPath = args[3];

            Console.WriteLine("Opening path file...");
            var lines = File.ReadAllLines(pathFile);
            Console.WriteLine("Opening Arc...");
            Arc arc = new Arc(arcPath);
            Console.WriteLine("Extracting...");
            foreach (var line in lines)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"Entry '{line}' -> ");
                Console.ForegroundColor = ConsoleColor.White;
                try
                {
                    var file = arc.GetFile(line);
                    var path = Path.Combine(extractPath, line);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, file);
                    Console.Write("Extracted");
                }
                catch (Exception e)
                {
                    Console.Write($"Failed: {e.Message}");
                }
                Console.WriteLine();
            }
        }

        static void Inject(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("Insufficient args. See -h for help");
                return;
            }
            string arcPath = args[1];
            string modsPath = args[2];
            string injectPath = args[3];

            Console.WriteLine("Opening mods directory...");
            DirectoryInfo info = new DirectoryInfo(modsPath);
            if (!File.Exists(injectPath))
            {
                Console.WriteLine($"Copying Arc to {injectPath}...");
                File.Copy(arcPath, injectPath);
            }
            Console.WriteLine("Opening Arc...");
            Arc arc = new Arc(arcPath);
            Console.WriteLine("Injecting mods...");
            
            using (BinaryWriter writer = new BinaryWriter(File.OpenWrite(injectPath)))
                RecursiveInject(arc, info, writer, "");
        }

        static void RecursiveInject(Arc arc, DirectoryInfo directory, BinaryWriter writer, string relativePath)
        {
            foreach (var folder in directory.EnumerateDirectories())
                RecursiveInject(arc, folder, writer, Path.Combine(relativePath, folder.Name));
            foreach (var file in directory.EnumerateFiles())
            {
                string path = Path.Combine(relativePath, file.Name).Replace('\\','/');
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"Entry '{path}' -> ");
                Console.ForegroundColor = ConsoleColor.White;
                try
                {
                    arc.GetFileInformation(path, out long offset, out uint compSize, out uint decompSize, out bool regional);
                    writer.BaseStream.Position = offset;
                    if (file.Length > decompSize)
                        throw new Exception($"Decompiled size exceeds its limit ({decompSize})");

                    byte[] inputFile = File.ReadAllBytes(file.FullName);
                    byte[] compFile = new byte[0];
                    bool canPad = false;
                    long padSize = 0;
                    for (int i = 1; i < 23; i++)
                    {
                        using (var memStream = new MemoryStream())
                        using (var zstdStream = new ZstandardStream(memStream, i))
                        {
                            zstdStream.Write(inputFile, 0, inputFile.Length);
                            zstdStream.Close();
                            compFile = memStream.ToArray();
                        }

                        padSize = compSize - compFile.Length;
                        if (padSize < 0 || padSize == 1 || padSize == 2 || padSize == 5)
                            continue;
                        canPad = true;
                        break;
                    }
                    if (!canPad)
                        throw new Exception("File unable to be compressed to the correct size");

                    //padding mechanism by Birdwards: https://github.com/Birdwards/SmashPad/blob/master/smashpad.py
                    byte Frame_Header_Descriptor = compFile[4];

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
                        compWithPadStream.Write(compFile, 0, start_index);

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
                            compWithPadStream.Write(padData);
                        }
                        else if (padSize % 3 == 2)
                        {
                            for (int i = 0; i < padSize - 8; i++)
                                compWithPadStream.WriteByte(0);
                            compWithPadStream.Write(padData);
                            compWithPadStream.Write(padData);
                        }

                        compWithPadStream.Write(compFile, start_index, compFile.Length - start_index);

                        if (compWithPadStream.Length != compSize)
                            throw new Exception("Error occurred in compression step, compression size mismatch");

                        writer.Write(compWithPadStream.ToArray());
                    }

                    Console.WriteLine("Injected");
                }
                catch (Exception e)
                {
                    Console.Write($"Failed: {e.Message}");
                }
                Console.WriteLine();
            }
        }
    }

    enum Mode
    {
        Extract,
        Inject
    }
}
