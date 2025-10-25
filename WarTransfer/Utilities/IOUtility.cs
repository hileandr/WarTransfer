using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WarTransfer
{
    internal static class IOUtility
    {
        internal static string[] GetFiles(string directory, params string[] patterns)
        {
            if (patterns == null || patterns.Length == 0)
                return Directory.GetFiles(directory);
            else if (patterns.Length == 1)
                return Directory.GetFiles(directory, patterns[0]);
            else
            {
                return patterns.SelectMany(x => Directory.GetFiles(directory, x)).Distinct().ToArray();
            }
        }

        internal static string[] GetDirectories(string directory, params string[] patterns)
        {
            if (patterns == null || patterns.Length == 0)
                return Directory.GetDirectories(directory);
            else if (patterns.Length == 1)
                return Directory.GetDirectories(directory, patterns[0]);
            else
            {
                return patterns.SelectMany(x => Directory.GetDirectories(directory, x)).Distinct().ToArray();
            }
        }

        internal static int CountFilesAndDirectories(string directory, params string[] patterns)
        {
            string[] mapFiles = GetFiles(directory, patterns);
            string[] mapDirectories = GetDirectories(directory, patterns);

            return mapFiles.Length + mapDirectories.Length;
        }

        internal static int CountFilesDeep(IWorkflowHandler e, string directory)
        {
            int count = 0;
            try
            {
                count = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Count();
            }
            catch (Exception ex)
            {
                e.ReportError(ex, false);
            }

            return count;
        }

        internal static bool CopyFile(IWorkflowHandler e, string sourceFile, string destinationFile)
        {
            try
            {
                File.Copy(sourceFile, destinationFile, true);
            }
            catch (Exception ex)
            {
                e.ReportError(ex, false);
                return false;
            }

            return true;
        }

        internal static bool CopyDirectory(IWorkflowHandler e, string sourceDir, string destDir)
        {
            bool result = true;

            try
            {
                if (!e.KillProcess)
                {
                    Directory.CreateDirectory(destDir);
                    foreach (string directory in Directory.EnumerateDirectories(sourceDir))
                    {
                        string directoryName = Path.GetFileName(directory);
                        result &= CopyDirectory(e, directory, Path.Combine(destDir, directoryName));
                    }

                    foreach (string file in Directory.EnumerateFiles(sourceDir))
                    {
                        string fileName = Path.GetFileName(file);
                        File.Copy(file, Path.Combine(destDir, fileName), true);
                    }
                }
            }
            catch (Exception ex)
            {
                e.ReportError(ex, false);
                result = false;
            }

            return result;
        }

        internal static bool EnsureDirectoryExistence(IWorkflowHandler e, string directory)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            catch (Exception ex)
            {
                e.ReportError(ex, false);
                return false;
            }

            return true;
        }

        internal static bool DeleteFile(IWorkflowHandler e, string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                e.ReportError(ex, false);
                return false;
            }

            return true;
        }

        internal static bool DeleteDirectory(IWorkflowHandler e, string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch (Exception ex)
            {
                e.ReportError(ex, false);
                return false;
            }

            return true;
        }

        internal static bool MoveDirectory(IWorkflowHandler e, string sourceDirectory, string targetDirectory)
        {
            try
            {
                if (Directory.Exists(targetDirectory))
                {
                    DeleteDirectory(e, targetDirectory);
                }

                if (!e.KillProcess)
                {
                    Directory.Move(sourceDirectory, targetDirectory);
                }
            }
            catch (Exception ex)
            {
                e.ReportError(ex, false);
                return false;
            }

            return true;
        }

        internal static int RunProcessCommands(IWorkflowHandler e, string programName, params string[] commands)
        {
            int exitCode = 0;

            if (commands.Length == 1)
            {
                exitCode = RunProcessCommand(e, programName, commands[0]);
            }
            else if (commands.Length > 0)
            {
                string fullCommandText = "/C ";
                for (int i = 0; i < commands.Length; i++)
                {
                    if (i != 0)
                        fullCommandText += "&";
                    fullCommandText += "(" + programName + " " + commands[i] + ")";
                }


                exitCode = RunProcessCommand(e, "CMD.exe", fullCommandText);
            }

            return exitCode;
        }

        internal static int RunProcessCommand(IWorkflowHandler e, string programName, string command)
        {
            Process process = null;
            int exitCode = 0;

            try
            {
                process = new Process();
                process.StartInfo = new ProcessStartInfo(programName, command);
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.Start();

                process.WaitForExit();
                exitCode = process.ExitCode;

                process.Close();
            }
            catch (Exception ex)
            {
                e.ReportError(ex, true);
                exitCode = -1;
            }
            finally
            {
                if (process != null)
                {
                    process.Dispose();
                }
            }

            return exitCode;
        }

        internal static bool TestFileAccess(string path)
        {
            bool isReadable = false;
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    isReadable = true;
                }
            }
            catch
            {
            }

            return isReadable;
        }
    }
}
