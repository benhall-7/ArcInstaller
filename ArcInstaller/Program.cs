using ArcCross;
using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using Zstandard.Net;

namespace ArcInstaller
{
    class Program
    {
        static string HelpText { get; set; } =
            "~ ArcInstaller ~\n" +
            "Usage: <mode> <options>\n" +
            "[Modes] = '-h' (print help)\n" +
            "        = '-i' (Inject)\n" +
            "        = '-e' (Extract)\n" +
            "        = '-f' (FTP)\n" +
            "\n" +
            "Extract <path to Arc>\n" +
            "        <path to name table>\n" +
            "        Optional: <extract folder>\n" +
            "\n" +
            "Inject <path to Arc>\n" +
            "       <path to mods folder>\n" +
            "       <path to output Arc>\n" +
            "         (if it does not exist, copies input arc)\n" +
            "       Optional: '-u'\n" +
            "         \"Undo\". takes all names in the directory and\n" +
            "         applies the original version into the new arc\n" +
            "       Or: '-d'\n" +
            "         \"Dump\". instead of injecting, copies compressed\n" +
            "         mods to an output folder specifed by the\n" +
            "         output path variable\n" +
            "\n" +
            "FTP <path to Arc>\n" +
            "    <path to mods folder>\n" +
            "    <IPv4 addr to switch server>\n" +
            "    <port number in switch server>\n" +
            "    Optional: <folder path>\n" +
            "      specifies a folder path in the switch to transfer to\n" +
            "      default: \"SaltySD/mods/\"";

        static HashSet<long> InjectedOffsets { get; set; }
        static bool InjectUndo { get; set; } = false;
        static bool InjectDump { get; set; } = false;
        static string InjectDumpPath { get; set; }

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
                    Inject(args);
                    break;
                case "-e":
                    Extract(args);
                    break;
                case "-f":
                    FTP(args);
                    break;
                case "-h":
                    Console.WriteLine(HelpText);
                    return;
                default:
                    Console.WriteLine($"Invalid option '{args[0]}'. See option '-h' for help");
                    return;
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
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(line);
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(" -> ");
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
            InjectedOffsets = new HashSet<long>();
            if (args.Length < 4)
            {
                Console.WriteLine("Insufficient args. See -h for help");
                return;
            }
            string arcPath = args[1];
            string modsPath = args[2];
            string injectPath = args[3];

            if (args.Length > 4)
            {
                if (args[4] == "-u")
                    InjectUndo = true;
                else if (args[4] == "-d")
                {
                    InjectDump = true;
                    InjectDumpPath = injectPath;
                }
                else
                {
                    Console.WriteLine($"Invalid option: {args[4]}");
                    return;
                }
            }

            Console.WriteLine("Opening mods directory...");
            DirectoryInfo info = new DirectoryInfo(modsPath);

            if (InjectDump)
            {
                if (!Directory.Exists(injectPath))
                {
                    Console.WriteLine($"Could not find path to dump compressed files at '{injectPath}'");
                    return;
                }
            }
            else
            {
                if (Path.GetFullPath(arcPath) == Path.GetFullPath(injectPath))
                {
                    Console.WriteLine("Path to arc and path to output arc cannot be the same.");
                    return;
                }
                if (InjectUndo)
                {
                    if (!File.Exists(injectPath))
                    {
                        Console.WriteLine($"Path does not exist: {injectPath}");
                        Console.WriteLine("There is no data to restore.");
                        return;
                    }
                }
                else
                {
                    if (!File.Exists(injectPath))
                    {
                        Console.WriteLine($"Copying Arc to {injectPath}. This may take some minutes...");
                        File.Copy(arcPath, injectPath);
                    }
                }
            }
            Console.WriteLine("Opening Arc...");
            Arc arc = new Arc(arcPath);
            Console.WriteLine("Injecting mods...");

            if (!InjectDump)
            {
                using (BinaryWriter writer = new BinaryWriter(File.OpenWrite(injectPath)))
                    RecursiveInject(arc, info, writer, "");
            }
            else
            {
                RecursiveInject(arc, info, null, "");
            }
        }

        static void RecursiveInject(Arc arc, DirectoryInfo directory, BinaryWriter writer, string relativePath)
        {
            foreach (var folder in directory.EnumerateDirectories())
                RecursiveInject(arc, folder, writer, Path.Combine(relativePath, folder.Name));
            foreach (var file in directory.EnumerateFiles())
            {
                string path = Path.Combine(relativePath, file.Name).Replace('\\', '/');
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(path);
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(" -> ");
                try
                {
                    arc.GetFileInformation(path, out long offset, out uint compSize, out uint decompSize, out bool regional);

                    if (offset == 0)
                        throw new Exception("File path does not return valid data. See if the path is correct");

                    if (InjectedOffsets.Contains(offset))
                        throw new Exception("File path points to address where data is already handled");

                    if (InjectUndo)
                    {
                        writer.BaseStream.Position = offset;
                        writer.Write(arc.GetFileCompressed(path));

                        InjectedOffsets.Add(offset);

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Restored");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    else if (InjectDump)
                    {
                        if (file.Length > decompSize)
                            throw new Exception($"Decompiled size ({file.Length}) exceeds its limit: ({decompSize})");

                        InjectedOffsets.Add(offset);
                        var compFile = Compress(file, compSize);
                        var dumpFolderPath = Path.Combine(InjectDumpPath, relativePath);
                        var newName = $"{offset.ToString("x")}_{file.Name}";
                        Directory.CreateDirectory(dumpFolderPath);
                        File.WriteAllBytes(Path.Combine(dumpFolderPath, newName), compFile);
                    }
                    else
                    {
                        if (file.Length > decompSize)
                            throw new Exception($"Decompiled size ({file.Length}) exceeds its limit: ({decompSize})");

                        writer.BaseStream.Position = offset;
                        writer.Write(Compress(file, compSize));

                        InjectedOffsets.Add(offset);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Injected");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("Failed: ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(e.Message);
                }
                Console.WriteLine();
            }
        }

        static void FTP(string[] args)
        {
            InjectedOffsets = new HashSet<long>();
            if (args.Length < 5)
            {
                Console.WriteLine("Insufficient args. See -h for help");
                return;
            }
            string arcPath = args[1];
            string modsPath = args[2];
            string ip = args[3];
            string port = args[4];
            string folder = "SaltySD/mods";
            if (args.Length > 5)
                folder = args[5];

            string ftpRoot = $"ftp://{ip}:{port}/{folder}/";

            Console.WriteLine($"FTP path: {ftpRoot}");

            Console.WriteLine("Opening mods directory...");
            DirectoryInfo info = new DirectoryInfo(modsPath);

            Console.WriteLine("Opening Arc...");
            Arc arc = new Arc(arcPath);

            RecursiveFTPFiles(arc, info, ftpRoot, "");
        }

        static void RecursiveFTPFiles(Arc arc, DirectoryInfo directory, string ftpRoot, string relativePath)
        {
            foreach (var folder in directory.EnumerateDirectories())
            {
                string thisRelPath = Path.Combine(relativePath, folder.Name);

                var req = (FtpWebRequest)WebRequest.Create(ftpRoot + thisRelPath.Replace('\\','/'));
                req.Method = "MKD";
                req.KeepAlive = true;
                req.Timeout = 10000;
                Console.WriteLine($"Requesting folder creation: {thisRelPath}");
                using (var res = (FtpWebResponse)req.GetResponse())
                    Console.WriteLine(res.StatusDescription);

                RecursiveFTPFiles(arc, folder, ftpRoot, thisRelPath);
            }
            foreach (var file in directory.EnumerateFiles())
            {
                string path = Path.Combine(relativePath, file.Name).Replace('\\', '/');
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(path);
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(" -> ");
                try
                {
                    arc.GetFileInformation(path, out long offset, out uint compSize, out uint decompSize, out bool regional);

                    if (offset == 0)
                        throw new Exception("File path does not return valid data. See if the path is correct");

                    if (InjectedOffsets.Contains(offset))
                        throw new Exception("File path points to address where data is already handled");

                    if (file.Length > decompSize)
                        throw new Exception($"Decompiled size ({file.Length}) exceeds its limit: ({decompSize})");

                    byte[] compFile = Compress(file, compSize);

                    var filepath = Path.Combine(relativePath, $"{offset.ToString("x")}_{file.Name}");
                    var ftpPath = ftpRoot + filepath.Replace('\\', '/');

                    var req = (FtpWebRequest)WebRequest.Create(ftpPath);
                    req.Method = "STOR";
                    req.KeepAlive = true;
                    req.UseBinary = true;
                    req.Timeout = 10000;
                    req.ContentLength = compFile.Length;

                    using (var str = req.GetRequestStream())
                        str.Write(compFile, 0, compFile.Length);
                    using (var res = (FtpWebResponse)req.GetResponse())
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Transfer status {res.StatusDescription}");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("Failed: ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(e.Message);
                }
                Console.WriteLine();
            }
        }

        static byte[] Compress(FileInfo file, uint compSize)
        {
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

                return compWithPadStream.ToArray();
            }
        }
    }
}
