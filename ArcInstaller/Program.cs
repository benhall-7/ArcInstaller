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
            Console.WriteLine("Opened Arc succesfully. Extracting...");
            Console.WriteLine();
            foreach (var line in lines)
            {
                Console.Write($"Entry '{line}' -> ");
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
            string arcPath = null;
            Console.WriteLine("Stubbed operation");
        }
    }

    enum Mode
    {
        Extract,
        Inject
    }
}
