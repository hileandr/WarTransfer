using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace WarTransfer
{
    internal static class MPQUtility
    {
        private static readonly Regex UnknownName = new Regex(@"^File[0-9]{8}\.[^\\/]+$", RegexOptions.IgnoreCase);
        internal static bool VerifyMPQEditor(string editorPath) => File.Exists(editorPath);

        internal static bool MapFileToMapDirectory(IWorkflowHandler e, string mapFile, string directoryPath, string editorPath)
        {
            if (e.KillProcess || !IOUtility.DeleteDirectory(e, directoryPath)) return false;
            int exitCode = IOUtility.RunProcessCommand(e, editorPath, $"extract \"{mapFile}\" * \"{directoryPath}\" /fp");
            if (exitCode != 0)
            {
                e.ReportError($"Error extracting map '{mapFile}'. MPQEditor exit code {exitCode}.", true);
                return false;
            }
            return !e.KillProcess;
        }

        internal static bool MapDirectoryToMapFile(IWorkflowHandler e, string mapDirectory, string filePath,
            string editorPath, string sourceArchive = null, string sourceDirectory = null)
        {
            string work = Path.Combine(Path.GetTempPath(), "WarTransfer", "pack-" + Guid.NewGuid().ToString("N"));
            string candidate = null;
            try
            {
                if (e.KillProcess) return false;
                string root = Path.GetFullPath(mapDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
                bool hasUnknownNames = files.Any(f => UnknownName.IsMatch(f.Substring(root.Length)));
                filePath = Path.GetFullPath(filePath);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                Directory.CreateDirectory(work);
                candidate = Path.Combine(Path.GetDirectoryName(filePath), ".wartransfer-" + Guid.NewGuid().ToString("N") + ".w3x");
                int exitCode;
                if (hasUnknownNames)
                {
                    // File########.ext is an extraction placeholder, not an original
                    // archive path. Rebuilding loses its hashes and can stop MPQEditor
                    // from adding all subsequent files, despite a zero exit code.
                    if (sourceArchive == null || sourceDirectory == null)
                        throw new InvalidDataException("Unnamed MPQ entries require the original source archive; they cannot be safely rebuilt from an extracted folder.");
                    File.Copy(sourceArchive, candidate);
                    string overlay = Path.Combine(work, "overlay");
                    Directory.CreateDirectory(overlay);
                    int changes = 0;
                    foreach (string file in files)
                    {
                        if (e.KillProcess) return false;
                        string relative = file.Substring(root.Length);
                        if (IsArchiveMetadata(relative)) continue;
                        string original = Path.Combine(sourceDirectory, relative);
                        if (File.Exists(original) && SameContents(file, original)) continue;
                        if (UnknownName.IsMatch(relative))
                            throw new InvalidDataException("Cannot modify unnamed archive entry: " + relative);
                        string destination = Path.Combine(overlay, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        File.Copy(file, destination);
                        changes++;
                    }
                    // Do not compact or resize: either can invalidate unnamed entries.
                    e.Log(LogType.UserInfo, $"Preserving unnamed source assets; updating {changes} changed files.");
                    exitCode = changes == 0 ? 0 : IOUtility.RunProcessCommand(e, editorPath, $"a \"{candidate}\" \"{overlay}\" /c /r");
                }
                else
                {
                    int capacity = Math.Max(NextPowerOf2(checked(files.Length + 3)), 4096);
                    exitCode = IOUtility.RunProcessCommands(e, editorPath,
                        $"n \"{candidate}\" {capacity}", $"a \"{candidate}\" \"{mapDirectory}\" /c /r");
                }
                if (exitCode != 0)
                    throw new IOException("MPQEditor returned exit code " + exitCode);
                if (!VerifyArchive(e, root, candidate, editorPath)) return false;
                if (e.KillProcess) return false;
                if (File.Exists(filePath)) File.Replace(candidate, filePath, null);
                else File.Move(candidate, filePath);
                return true;
            }
            catch (Exception ex)
            {
                e.ReportError($"Error packing map '{filePath}': {ex.Message}", true);
                return false;
            }
            finally
            {
                if (candidate != null && !IOUtility.DeleteFile(e, candidate)) e.KillProcess = true;
                if (!IOUtility.DeleteDirectory(e, work)) e.KillProcess = true;
            }
        }

        internal static bool VerifyArchive(IWorkflowHandler e, string mapDirectory, string archive, string editorPath)
        {
            string verification = Path.Combine(Path.GetTempPath(), "WarTransfer", "verify-" + Guid.NewGuid().ToString("N"));
            try
            {
                e.Log(LogType.UserInfo, "Verifying packed map contents...");
                if (!MapFileToMapDirectory(e, archive, verification, editorPath)) return false;
                string root = Path.GetFullPath(mapDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                foreach (string source in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    if (e.KillProcess) return false;
                    string relative = source.Substring(root.Length);
                    if (IsArchiveMetadata(relative)) continue;
                    string extracted = Path.Combine(verification, relative);
                    if (!File.Exists(extracted) || !SameContents(source, extracted))
                    {
                        e.ReportError($"Packed map verification failed: '{relative}' is missing or changed. MPQEditor may have silently omitted files. Output was not published.", true);
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                e.ReportError($"Cannot verify packed map '{archive}': {ex.Message}", true);
                return false;
            }
            finally
            {
                if (!IOUtility.DeleteDirectory(e, verification)) e.KillProcess = true;
            }
        }

        private static bool IsArchiveMetadata(string path) => path.Equals("(listfile)", StringComparison.OrdinalIgnoreCase)
            || path.Equals("(attributes)", StringComparison.OrdinalIgnoreCase) || path.Equals("(signature)", StringComparison.OrdinalIgnoreCase);

        private static bool SameContents(string first, string second)
        {
            if (new FileInfo(first).Length != new FileInfo(second).Length) return false;
            using (var hash = SHA256.Create())
            using (var a = File.OpenRead(first))
            using (var b = File.OpenRead(second))
                return hash.ComputeHash(a).SequenceEqual(hash.ComputeHash(b));
        }

        private static int NextPowerOf2(int n)
        {
            if (n < 1 || n > (1 << 29)) throw new ArgumentOutOfRangeException(nameof(n));
            n--;
            n |= n >> 1; n |= n >> 2; n |= n >> 4; n |= n >> 8; n |= n >> 16;
            return n + 1;
        }
    }
}
