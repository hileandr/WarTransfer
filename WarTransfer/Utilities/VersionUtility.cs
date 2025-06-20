using System;
using System.IO;
using System.Text.RegularExpressions;
using WarTransfer.FileTypes;

namespace WarTransfer
{
    internal static class VersionUtility
    {
        private static Regex s_versionRegex = null;
        private static string s_version = null;

        internal static void SetVersionNumberPattern(string versionNumberPattern)
        {
            if (!string.IsNullOrWhiteSpace(versionNumberPattern))
            {
                s_versionRegex = new Regex(versionNumberPattern);
            }
            else
            {
                s_versionRegex = null;
            }
        }

        internal static void UpdateVersion(string mapName, string versionNumberPattern)
        {
            SetVersionNumberPattern(versionNumberPattern);

            string version = ParseVersion(mapName, true);
            if (version.Length > 0)
            {
                s_version = version;
            }
        }

        internal static string ParseVersion(string mapName, bool isFileName)
        {
            string version = "";
            if (s_versionRegex != null)
            {
                if (isFileName)
                {
                    mapName = Path.GetFileName(mapName);

                    if (Path.HasExtension(mapName))
                    {
                        mapName = Path.ChangeExtension(mapName, null);
                    }
                }

                foreach (Match match in s_versionRegex.Matches(mapName))
                {
                    if (match.Success && match.Length > 0 && match.Groups.Count > 0)
                    {
                        version = match.Groups[match.Groups.Count - 1].Value;
                        break;
                    }
                }
            }

            return version;
        }

        internal static string ChangeFileNameVersion(string filePath)
        {
            if (!string.IsNullOrEmpty(s_version))
            {
                string fileName = Path.GetFileName(filePath);
                string directoryName = Path.GetDirectoryName(filePath);

                string version = ParseVersion(fileName, true);
                if (!string.IsNullOrWhiteSpace(version) && !version.Equals(s_version))
                {
                    string extension = Path.GetExtension(filePath);
                    string fileNoExtension = Path.ChangeExtension(fileName, null);

                    string updatedFileName = fileNoExtension.Substring(0, fileNoExtension.Length - version.Length) + s_version + extension;
                    return Path.Combine(directoryName, updatedFileName);
                }
            }

            return filePath;
        }

        internal static string ChangeVersion(WtsFile stringFile, string description)
        {
            if (!string.IsNullOrEmpty(s_version))
            {
                if (StringUtility.IsRef(description))
                {
                    int key = StringUtility.ExtractRefKey(description);
                    string[] lines = stringFile.FileMap[key];
                    for (int i = 1; i < lines.Length; i++)
                    {
                        string oldVersion = ParseVersion(lines[i], false);
                        if (!string.IsNullOrEmpty(oldVersion))
                        {
                            lines[i] = lines[i].Replace(oldVersion, s_version);
                        }
                    }
                }
                else
                {
                    string oldVersion = ParseVersion(description, false);
                    if (!string.IsNullOrEmpty(oldVersion))
                    {
                        return description.Replace(oldVersion, s_version);
                    }
                }
            }

            return description;
        }
    }
}
