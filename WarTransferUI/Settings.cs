using WarTransfer;
using WarTransfer.FileTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WarTransferUI
{
    public static class Settings
    {
        private const string SETTINGS_DIRECTORY = "Data";
        private const string SETTINGS_FILE_NAME = "Settings.ini";

        public static string LastSourceMap { get; set; }
        public static string LastSourceDirectory { get; set; }
        public static string LastOutputDirectory { get; set; }
        public static bool TransferUnitTables { get; set; }
        public static bool TransferItemTables { get; set; }
        public static bool TransferCameras { get; set; }
        public static bool TransferRegions { get; set; }
        public static W3iFlags TransferredFlags { get; set; }
        public static bool EnableVersioning { get; set; }
        public static bool EnableUISounds { get; set; }
        public static string VersionNumberRegex { get; set; }

        private static PropertyInfo[] SettingsProps;

        static Settings()
        {
            LastSourceMap = "";
            LastSourceDirectory = "";
            LastOutputDirectory = "";
            TransferUnitTables = false;
            TransferItemTables = false;
            TransferCameras = false;
            TransferRegions = false;
            TransferredFlags = W3iFlags.MeleeMap;
            EnableVersioning = false;
            EnableUISounds = true;
            VersionNumberRegex = "\\d+\\.\\d+(?:\\.\\d+)*";

            SettingsProps = typeof(Settings).GetProperties(BindingFlags.Public | BindingFlags.Static);
        }

        private static string SettingsPath
        {
            get
            {
                return Path.Combine(SETTINGS_DIRECTORY, SETTINGS_FILE_NAME);
            }
        }

        public static void SaveSettings(IWorkflowHandler e)
        {
            try
            {
                if (!Directory.Exists(SETTINGS_DIRECTORY))
                {
                    Directory.CreateDirectory(SETTINGS_DIRECTORY);
                }

                var settingsValues = GetSettingsValues();

                using (FileStream file = new FileStream(SettingsPath, FileMode.Create))
                {
                    using (StreamWriter writer = new StreamWriter(file))
                    {
                        writer.WriteLine("[Settings]");
                        foreach (var settingsPair in settingsValues)
                        {
                            writer.WriteLine($"{settingsPair.Item1}={settingsPair.Item2}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                e.ReportError($"Error while saving settings: {ex.Message}", false);
            }
        }

        public static void LoadSettings(IWorkflowHandler e)
        {
            try
            {
                if (!Directory.Exists(SETTINGS_DIRECTORY))
                {
                    Directory.CreateDirectory(SETTINGS_DIRECTORY);
                }

                if (File.Exists(SettingsPath))
                {
                    using (FileStream file = new FileStream(SettingsPath, FileMode.Open))
                    {
                        using (StreamReader reader = new StreamReader(file))
                        {
                            string? line;
                            do
                            {
                                line = reader.ReadLine();
                            }
                            while (line != null && !line.Equals("[Settings]"));

                            while ((line = reader.ReadLine()) != null)
                            {
                                ReadSetting(line);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                e.ReportError($"Error while loading settings: {ex.Message}", false);
            }
        }

        private static List<(string, string)> GetSettingsValues()
        {
            List<(string, string)> result = new List<(string, string)>();

            foreach (PropertyInfo prop in SettingsProps)
            {
                string value = "";
                string? settingVal = null;
                if (prop.PropertyType.IsEnum)
                {
                    settingVal = Convert.ChangeType(prop.GetValue(null), prop.PropertyType.GetEnumUnderlyingType())?.ToString();
                }
                else
                {
                    settingVal = prop.GetValue(null)?.ToString();
                }
                
                if (settingVal != null)
                {
                    value = settingVal;
                }

                result.Add((prop.Name, value));
            }

            return result;
        }

        private static void ReadSetting(string s)
        {
            int indexOfEquals = s.IndexOf('=');
            if (indexOfEquals != -1)
            {
                string name = s.Substring(0, indexOfEquals);
                object value = s.Substring(indexOfEquals + 1);

                foreach (PropertyInfo prop in SettingsProps)
                {
                    if (prop.Name.Equals(name))
                    {
                        if (prop.PropertyType.IsEnum)
                        {
                            value = Convert.ChangeType(value, prop.PropertyType.GetEnumUnderlyingType());
                        }
                        else if (prop.PropertyType != typeof(string))
                        {
                            value = Convert.ChangeType(value, prop.PropertyType);
                        }

                        prop.SetValue(null, value);
                        break;
                    }
                }
            }
        }
    }
}
