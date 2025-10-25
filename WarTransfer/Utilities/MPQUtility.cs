using System;
using System.Diagnostics;
using System.IO;

namespace WarTransfer
{
    internal static class MPQUtility
    {
        internal static bool VerifyMPQEditor()
        {
            return File.Exists("MPQEditor.exe");
        }

        internal static bool MapFileToMapDirectory(IWorkflowHandler e, string mapFile, string directoryPath)
        {
            int exitCode = 0;

            // Remove any existing maps
            if (IOUtility.DeleteDirectory(e, directoryPath))
            {

                if (!e.KillProcess)
                {
                    // Extract the map file to our target directory
                    exitCode = IOUtility.RunProcessCommand(e, "MPQEditor.exe", $"extract \"{mapFile}\" * \"{directoryPath}\" /fp");
                }

                if (exitCode != 0)
                {
                    e.ReportError($"Error copying map file \"{mapFile}\" to \"{directoryPath}\". Error code {exitCode}.", false);
                }

                return exitCode == 0;
            }

            return false;
        }

        internal static bool MapDirectoryToMapFile(IWorkflowHandler e, string mapDirectory, string filePath, int fileCount)
        {
            int exitCode = 0;

            // Remove any existing maps
            if (IOUtility.DeleteFile(e, filePath))
            {
                if (!e.KillProcess)
                {
                    int maxFileLimit = Math.Max(NextPowerOf2(fileCount), 4096); // 4096 is the default value in MPQEditor

                    string[] commands = new string[]
                    {
                        $"n \"{filePath}\" {maxFileLimit}",                     // Create a blank .w3x file
                        $"a \"{filePath}\" \"{mapDirectory}\" /c /auto /r",     // Add our directory files to the .w3x archive
                        $"c"                                                    // Close the .w3x archive.
                    };

                    exitCode = IOUtility.RunProcessCommands(e, "MPQEditor.exe", commands);
                }

                if (exitCode != 0)
                {
                    e.ReportError($"Error copying map file \"{mapDirectory}\" to \"{filePath}\". Error code {exitCode}.", false);
                }

                return exitCode == 0;
            }

            return false;
        }

        private static int NextPowerOf2(int n)
        {
            if (n < 1)
                return 1;

            n--;
            n |= n >> 1;
            n |= n >> 2;
            n |= n >> 4;
            n |= n >> 8;
            n |= n >> 16;
            n++;
            return n;
        }
    }
}
