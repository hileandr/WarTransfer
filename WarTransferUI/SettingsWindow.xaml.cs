using WarTransfer;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WarTransferUI
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private static readonly SolidColorBrush WhiteColor = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        private static readonly SolidColorBrush RegexSuccessColor = new SolidColorBrush(Color.FromRgb(128, 208, 128));
        private static readonly SolidColorBrush RegexFailColor = new SolidColorBrush(Color.FromRgb(208, 160, 160));

        public SettingsWindow()
        {
            InitializeComponent();

            EnableUISounds.IsChecked = Settings.EnableUISounds;
            TransferRegions.IsChecked = Settings.TransferRegions;
            TransferCameras.IsChecked = Settings.TransferCameras;
            EnableVersioning.IsChecked = Settings.EnableVersioning;
            VersionNumberRegex.Text = Settings.VersionNumberRegex;
            RegexPreviewInput.Text = "MySourceMap1.01.w3x";

            SetVersioningControlsEnabled(Settings.EnableVersioning);

            BindEvents();
        }

        private void BindEvents()
        {
            Cancel.Click += Cancel_Click;
            Save.Click += Save_Click;

            EnableVersioning.Checked += EnableVersioning_Checked;
            EnableVersioning.Unchecked += EnableVersioning_Unchecked;

            VersionNumberRegex.TextChanged += RegexInputChanged;
            VersionNumberInput.TextChanged += RegexInputChanged;
            RegexPreviewInput.TextChanged += RegexInputChanged;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            Hide();
            e.Cancel = true;
        }

        private void EnableVersioning_Checked(object sender, RoutedEventArgs e)
        {
            SetVersioningControlsEnabled(true);
        }

        private void EnableVersioning_Unchecked(object sender, RoutedEventArgs e)
        {
            SetVersioningControlsEnabled(false);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Settings.EnableUISounds = EnableUISounds.IsChecked.GetValueOrDefault(false);
            Settings.TransferRegions = TransferRegions.IsChecked.GetValueOrDefault(false);
            Settings.TransferCameras = TransferCameras.IsChecked.GetValueOrDefault(false);
            Settings.EnableVersioning = EnableVersioning.IsChecked.GetValueOrDefault(false);
            Settings.VersionNumberRegex = VersionNumberRegex.Text;

            Settings.SaveSettings(AppHandler.Instance);

            Hide();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            EnableVersioning.IsChecked = Settings.EnableVersioning;
            VersionNumberRegex.Text = Settings.VersionNumberRegex;

            Hide();
        }

        private void RegexInputChanged(object sender, TextChangedEventArgs e)
        {
            UpdateVersionNumberRegex();
        }

        private void SetVersioningControlsEnabled(bool enabled)
        {
            VersionNumberRegex.IsEnabled = enabled;
            VersionNumberInput.IsEnabled = enabled;
            RegexPreviewInput.IsEnabled = enabled;
            RegexPreviewOutput.IsEnabled = enabled;

            UpdateVersionNumberRegex();
        }

        private bool IsValidRegexPattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return false;

            try
            {
                Regex.Match("", pattern);
            }
            catch
            {
                return false;
            }

            return true;
        }

        private bool ValidateVersionNumberRegex()
        {
            bool validRegex = IsValidRegexPattern(VersionNumberRegex.Text);
            if (validRegex)
            {
                VersionNumberRegex.Background = WhiteColor;
            }
            else
            {
                VersionNumberRegex.Background = RegexFailColor;
            }

            Save.IsEnabled = validRegex;

            return validRegex;
        }

        private bool ValidateVersionNumberInput(string pattern)
        {
            if (!pattern.StartsWith("^"))
                pattern = "^" + pattern;

            if (!pattern.EndsWith("$"))
                pattern += "$";

            if (!string.IsNullOrWhiteSpace(VersionNumberInput.Text) &&
                Regex.IsMatch(VersionNumberInput.Text, pattern))
            {
                VersionNumberInput.Background = WhiteColor;
                return true;
            }

            VersionNumberInput.Background = RegexFailColor;
            return false;
        }

        private void UpdateVersionNumberRegex()
        {
            if (EnableVersioning.IsChecked == true)
            {
                bool valid = ValidateVersionNumberRegex();
                valid = valid && ValidateVersionNumberInput(VersionNumberRegex.Text);

                if (valid && !string.IsNullOrEmpty(RegexPreviewInput.Text))
                {
                    Regex regex = new Regex(VersionNumberRegex.Text);

                    Match match = regex.Match(RegexPreviewInput.Text);
                    if (match.Success)
                    {
                        RegexPreviewOutput.Text = regex.Replace(RegexPreviewInput.Text, VersionNumberInput.Text);
                        RegexPreviewOutput.Background = RegexSuccessColor;
                    }
                    else
                    {
                        RegexPreviewOutput.Text = RegexPreviewInput.Text;
                        RegexPreviewOutput.Background = RegexFailColor;
                    }
                }
                else
                {
                    RegexPreviewOutput.Background = WhiteColor;
                    RegexPreviewOutput.Text = null;
                }
            }
            else
            {
                VersionNumberRegex.Background = WhiteColor;
                VersionNumberInput.Background = WhiteColor;
                RegexPreviewOutput.Background = WhiteColor;
                RegexPreviewOutput.Text = null;

                Save.IsEnabled = true;
            }
        }
    }
}
