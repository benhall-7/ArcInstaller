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
        public static readonly string[] RegionTags =
        {
            "+jp_ja",
            "+us_en",
            "+us_fr",
            "+us_es",
            "+eu_en",
            "+eu_fr",
            "+eu_es",
            "+eu_de",
            "+eu_nl",
            "+eu_it",
            "+eu_ru",
            "+kr_ko",
            "+zh_cn",
            "+zh_tw"
        };

        static string HelpText { get; set; } =
            "~ ArcInstaller ~\n" +
            "Usage: <mode> <options>\n" +
            "[Modes] '-h' (print help)\n" +
            "        '-i' (Inject)\n" +
            "        '-e' (Extract)\n" +
            "        '-f' (FTP)\n" +
            "        '--ftpdir' (FTP make directory)\n" +
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
            "         -Additional: '-1'\n" +
            "           stores all compressed mods in a single folder\n" +
            "\n" +
            "FTP <path to Arc>\n" +
            "    <path to mods folder>\n" +
            "    <IPv4 addr to switch server>\n" +
            "    <port number in switch server>\n" +
            "    Optional: '-o' <folder path>\n" +
            "      specifies a folder path in the switch to transfer to\n" +
            "      default: SaltySD/mods\n" +
            "      NOTE: doesn't begin or end with '/'\n" +
            "    Optional: '-1'\n" +
            "       stores all compressed mods in a single folder\n" +
            "\n" +
            "ftpdir <IPv4 addr to switch server>\n" +
            "       <port number in switch server>\n" +
            "       <directory to make>\n" +
            "         Ex: SaltySD/mods/MyModName";

        static HashSet<long> InjectedOffsets { get; set; }
        static bool InjectUndo { get; set; } = false;
        static bool InjectDump { get; set; } = false;
        static string InjectDumpPath { get; set; }
        static bool ToSingleFolder { get; set; } = false;

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
                case "--ftpdir":
                    FTP_MakeDir(args);
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

                    if (args.Length > 5)
                    {
                        if (args[5] == "-1")
                            ToSingleFolder = true;
                    }
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
                GetFileRegionInfo(file.Name, out string arcFileName, out int region);
                string arcPath = Path.Combine(relativePath, arcFileName).Replace('\\', '/').Replace(';', ':');
                
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(arcPath);
                if (region > 0)
                    Console.Write($" region={region}");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(" -> ");
                try
                {
                    arc.GetFileInformation(arcPath, out long offset, out uint compSize, out uint decompSize, out bool regional, region);

                    if (offset == 0)
                        throw new Exception("File path does not return valid data. See if the path is correct");

                    if (InjectedOffsets.Contains(offset))
                        throw new Exception($"Another file already has this offset ({offset.ToString("x")})");

                    if (InjectUndo)
                    {
                        writer.BaseStream.Position = offset;
                        writer.Write(arc.GetFileCompressed(arcPath));

                        InjectedOffsets.Add(offset);

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write("Restored");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    else if (InjectDump)
                    {
                        if (file.Length > decompSize)
                            throw new Exception($"Decompiled size ({file.Length}) exceeds its limit: ({decompSize})");

                        InjectedOffsets.Add(offset);
                        var compFile = Compress(file, compSize, decompSize);
                        if (!ToSingleFolder)
                        {
                            var dumpFolderPath = Path.Combine(InjectDumpPath, relativePath);
                            var newName = $"{offset.ToString("x")}_{file.Name}";
                            Directory.CreateDirectory(dumpFolderPath);
                            File.WriteAllBytes(Path.Combine(dumpFolderPath, newName), compFile);
                        }
                        else
                        {
                            var newName = $"{offset.ToString("x")}";
                            File.WriteAllBytes(Path.Combine(InjectDumpPath, newName), compFile);
                        }

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("Done");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    else
                    {
                        if (file.Length > decompSize)
                            throw new Exception($"Decompiled size ({file.Length}) exceeds its limit: ({decompSize})");

                        writer.BaseStream.Position = offset;
                        writer.Write(Compress(file, compSize, decompSize));

                        InjectedOffsets.Add(offset);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("Injected");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("Failed: ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write(e.Message);
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

            for (int i = 5; i < args.Length; i++)
            {
                if (args[i] == "-o")
                    folder = args[++i];
                else if (args[i] == "-1")
                    ToSingleFolder = true;
            }

            string ftpRoot = $"ftp://{ip}:{port}/{folder}/";

            Console.WriteLine($"FTP path: {ftpRoot}");

            Console.WriteLine("Opening mods directory...");
            DirectoryInfo info = new DirectoryInfo(modsPath);

            Console.WriteLine("Opening Arc...");
            Arc arc = new Arc(arcPath);

            RecursiveFTP(arc, info, ftpRoot, "");
        }

        static void RecursiveFTP(Arc arc, DirectoryInfo directory, string ftpRoot, string relativePath)
        {
            foreach (var folder in directory.EnumerateDirectories())
            {
                string thisRelPath = Path.Combine(relativePath, folder.Name);

                if (!ToSingleFolder)
                {
                    var req = (FtpWebRequest)WebRequest.Create(ftpRoot + thisRelPath.Replace('\\', '/'));
                    req.Method = "MKD";
                    req.KeepAlive = true;
                    req.Timeout = 10000;

                    Console.WriteLine($"Requesting make dir: {thisRelPath}");

                    try
                    {
                        using (var res = (FtpWebResponse)req.GetResponse())
                            Console.WriteLine(res.StatusDescription);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error: {e.Message}");
                    }
                }

                RecursiveFTP(arc, folder, ftpRoot, thisRelPath);
            }
            foreach (var file in directory.EnumerateFiles())
            {
                GetFileRegionInfo(file.Name, out string arcFileName, out int region);
                string arcPath = Path.Combine(relativePath, arcFileName).Replace('\\', '/').Replace(';', ':');

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(arcPath);
                if (region > 0)
                    Console.Write($" region={region}");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(" -> ");
                try
                {
                    arc.GetFileInformation(arcPath, out long offset, out uint compSize, out uint decompSize, out bool regional, region);

                    if (offset == 0)
                        throw new Exception("File path does not return valid data. See if the path is correct");

                    if (InjectedOffsets.Contains(offset))
                        throw new Exception("File path points to address where data is already handled");

                    if (file.Length > decompSize)
                        throw new Exception($"Decompiled size ({file.Length}) exceeds its limit: ({decompSize})");

                    if (InjectedOffsets.Contains(offset))
                        throw new Exception($"Another file already has this offset ({offset.ToString("x")})");

                    byte[] compFile = Compress(file, compSize, decompSize);

                    string filepath;
                    if (!ToSingleFolder)
                        filepath = Path.Combine(relativePath, $"{offset.ToString("x")}_{file.Name}");
                    else
                        filepath = $"{offset.ToString("x")}";
                    var ftpPath = ftpRoot + filepath.Replace('\\', '/');

                    var req = (FtpWebRequest)WebRequest.Create(ftpPath);
                    req.Method = "STOR";
                    req.KeepAlive = true;
                    req.UseBinary = true;
                    req.Timeout = 10000;
                    req.UsePassive = false;
                    req.ContentLength = compFile.Length;

                    Console.Write("Uploading... ");
                    using (var str = req.GetRequestStream())
                        str.Write(compFile, 0, compFile.Length);
                    using (var res = (FtpWebResponse)req.GetResponse())
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("Transferred!");
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($" (FTP status: {res.StatusDescription})");
                    }

                    InjectedOffsets.Add(offset);
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("Failed: ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write(e.Message);
                }
                Console.WriteLine();
            }
        }

        static byte[] Compress(FileInfo file, uint compSize, uint decompSize)
        {
            return null;
        }

        static void GetFileRegionInfo(string original, out string nameNoRegion, out int region)
        {
            region = 0;
            string noExt = Path.GetFileNameWithoutExtension(original);
            string ext = Path.GetExtension(original);
            nameNoRegion = noExt;
            int plusIndex = noExt.LastIndexOf('+');
            if (plusIndex >= 0)//-1 = there is no + char
            {
                for (int i = 0; i < RegionTags.Length; i++)
                {
                    if (noExt.EndsWith(RegionTags[i]))
                    {
                        region = i;
                        nameNoRegion = noExt.Remove(plusIndex);
                        break;
                    }
                }
            }
            nameNoRegion += ext;
        }

        static void FTP_MakeDir(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("Insufficient args. See -h for help");
                return;
            }
            string ip = args[1];
            string port = args[2];
            string folder = args[3];

            string ftpPath = $"ftp://{ip}:{port}/";

            foreach (var dir_part in folder.Split('/'))
            {
                ftpPath += dir_part;

                var req = (FtpWebRequest)WebRequest.Create(ftpPath);
                req.Method = "MKD";
                req.KeepAlive = true;
                req.Timeout = 10000;

                Console.WriteLine($"Requesting make dir: {ftpPath}");

                try
                {
                    using (var res = (FtpWebResponse)req.GetResponse())
                        Console.WriteLine(res.StatusDescription);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error: {e.Message}");
                }

                ftpPath += '/';
            }
        }
    }
}
