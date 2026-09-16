using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WarTransfer;
using WarTransfer.FileTypes;

namespace WarTransferCL
{
    internal sealed class CliOptions
    {
        internal WarTransferArgs Transfer = new WarTransferArgs();
        internal bool Json, DryRun, Overwrite, Verbose, Help, Version;
        internal List<string> Outputs = new List<string>();
        internal List<string> Targets = new List<string>();

        internal static CliOptions Parse(string[] args)
        {
            var options = new CliOptions();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int start = args.Length > 0 && args[0] == "transfer" ? 1 : 0;
            for (int i = start; i < args.Length; i++)
            {
                string token = args[i];
                int equals = token.IndexOf('=');
                string key = equals < 0 ? token : token.Substring(0, equals);
                if (key == "-h") key = "--help";
                if (!seen.Add(key)) throw new ArgumentException("Duplicate option: " + key);
                bool takesValue = new[] { "--source", "--targets", "--output", "--mpq-editor", "--version-regex", "--flags" }.Contains(key);
                string value = null;
                if (takesValue)
                {
                    if (equals >= 0) value = token.Substring(equals + 1);
                    else if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) value = args[++i];
                    if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Missing value for " + key);
                }
                else if (equals >= 0) throw new ArgumentException("Option does not accept a value: " + key);
                switch (key)
                {
                    case "--source": options.Transfer.SourceMap = value; break;
                    case "--targets": options.Transfer.SourceDirectory = value; break;
                    case "--output": options.Transfer.OutputDirectory = value; break;
                    case "--mpq-editor": options.Transfer.MpqEditorPath = value; break;
                    case "--version-regex": options.Transfer.VersionNumberRegex = value; break;
                    case "--flags": options.Transfer.W3iFlagsToTransfer = ParseFlags(value); break;
                    case "--transfer-cameras": options.Transfer.TransferCameras = true; break;
                    case "--transfer-regions": options.Transfer.TransferRegions = true; break;
                    case "--transfer-unit-tables": options.Transfer.TransferUnitTables = true; break;
                    case "--transfer-item-tables": options.Transfer.TransferItemTables = true; break;
                    case "--json": options.Json = true; break;
                    case "--dry-run": options.DryRun = true; break;
                    case "--overwrite": options.Overwrite = true; break;
                    case "--verbose": options.Verbose = true; break;
                    case "--help": options.Help = true; break;
                    case "--version": options.Version = true; break;
                    default: throw new ArgumentException("Unknown option or command: " + key);
                }
            }
            return options;
        }

        private static W3iFlags ParseFlags(string text)
        {
            if (text.Equals("none", StringComparison.OrdinalIgnoreCase)) return 0;
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                uint mask;
                if (uint.TryParse(text.Substring(2), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out mask)) return (W3iFlags)mask;
                throw new ArgumentException("Invalid hexadecimal --flags mask.");
            }
            W3iFlags result = 0;
            foreach (var name in text.Split(','))
            {
                string trimmed = name.Trim();
                string match = Enum.GetNames(typeof(W3iFlags)).FirstOrDefault(n => n.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
                if (match == null) throw new ArgumentException("Unknown map flag: " + trimmed);
                result |= (W3iFlags)Enum.Parse(typeof(W3iFlags), match);
            }
            return result;
        }

        internal void Validate()
        {
            Transfer.SourceMap = FullPath(Transfer.SourceMap, "--source");
            Transfer.SourceDirectory = FullPath(Transfer.SourceDirectory, "--targets");
            Transfer.OutputDirectory = FullPath(Transfer.OutputDirectory, "--output");
            Transfer.MpqEditorPath = FullPath(Transfer.MpqEditorPath, "--mpq-editor");
            if (!File.Exists(Transfer.SourceMap) || !IsMap(Transfer.SourceMap))
                throw new ArgumentException("--source must be an existing .w3x or .w3m archive.");
            if (!Directory.Exists(Transfer.SourceDirectory)) throw new ArgumentException("--targets directory does not exist.");
            if (File.Exists(Transfer.OutputDirectory)) throw new ArgumentException("--output must be a directory.");
            RejectLinks(Transfer.SourceMap);
            RejectLinks(Transfer.SourceDirectory);
            RejectLinks(Transfer.OutputDirectory);
            if (Within(Transfer.OutputDirectory, Transfer.SourceDirectory) || Within(Transfer.SourceDirectory, Transfer.OutputDirectory)
                || Within(Transfer.SourceMap, Transfer.OutputDirectory))
                throw new ArgumentException("Output must be separate from the source map and target directory (no overlapping trees).");
            if (Directory.GetDirectories(Transfer.SourceDirectory).Any(IsMap))
                throw new ArgumentException("--targets must contain map archives, not extracted .w3x/.w3m directories.");
            Targets = Directory.GetFiles(Transfer.SourceDirectory).Where(IsMap)
                .Where(p => !p.Equals(Transfer.SourceMap, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            if (Targets.Count == 0) throw new ArgumentException("No target maps found. Only immediate .w3x/.w3m files are selected; the source is excluded.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFileNameWithoutExtension(Transfer.SourceMap) };
            foreach (var target in Targets)
            {
                RejectLinks(target);
                if (!names.Add(Path.GetFileNameWithoutExtension(target)))
                    throw new ArgumentException("Map names collide after .w3x normalization: " + target);
            }
            VersionUtility.UpdateVersion(Transfer.SourceMap, Transfer.VersionNumberRegex);
            if (!string.IsNullOrEmpty(Transfer.VersionNumberRegex) && string.IsNullOrEmpty(VersionUtility.ParseVersion(Transfer.SourceMap, true)))
                throw new ArgumentException("--version-regex must match a nonempty version in the source filename.");
            var outputNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var target in Targets)
            {
                string output = VersionUtility.ChangeFileNameVersion(Path.Combine(Transfer.OutputDirectory, Path.GetFileNameWithoutExtension(target) + ".w3x"));
                if (!outputNames.Add(output)) throw new ArgumentException("Versioning produces duplicate output: " + output);
                if (Directory.Exists(output)) throw new ArgumentException("An output map path is a directory: " + output);
                if (File.Exists(output) && !Overwrite) throw new ArgumentException("Output already exists; use --overwrite to replace it: " + output);
                RejectLinks(output);
                Outputs.Add(output);
            }
        }

        private static bool IsMap(string path) => new[] { ".w3x", ".w3m" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
        private static string FullPath(string path, string option)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Required option: " + option);
            string fullPath = Path.GetFullPath(path);
            return fullPath == Path.GetPathRoot(fullPath) ? fullPath : fullPath.TrimEnd(Path.DirectorySeparatorChar);
        }
        private static bool Within(string path, string root) => path.Equals(root, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        private static void RejectLinks(string path)
        {
            for (string current = path; current != null; current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new ArgumentException("Map and output paths must not traverse symbolic links or junctions: " + current);
        }
    }
}
