using WarTransfer;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WarTransferUI;

namespace WarCopyUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Task? m_DataTransferTask = null;
        private ObservableCollection<LogMessage> m_Logs;
        private UIWorkflowHandler m_WorkflowHandler;
        private bool m_AutoScroll = true;

        private SettingsWindow? m_SettingsWindow;

        public MainWindow()
        {
            InitializeComponent();

            m_Logs = new ObservableCollection<LogMessage>();
            m_WorkflowHandler = new UIWorkflowHandler();

            SourceMapPath.Text = Settings.LastSourceMap;
            TargetDirectoryPath.Text = Settings.LastSourceDirectory;
            OutputDirectoryPath.Text = Settings.LastOutputDirectory;

            Log.ItemsSource = m_Logs;

            UpdateGoButtonEnabled();

            BindEvents();
        }

        private void BindEvents()
        {
            SourceMapPath.TextChanged += SourceMapPath_TextChanged;
            TargetDirectoryPath.TextChanged += TargetDirPath_TextChanged;
            OutputDirectoryPath.TextChanged += OutputDirPath_TextChanged;

            SourceMapBrowse.Click += SourceMapBrowse_Click;
            TargetDirectoryBrowse.Click += TargetDirectoryBrowse_Click;
            OutputDirectoryBrowse.Click += OutputDirectoryBrowse_Click;

            LogViewer.ScrollChanged += LogViewer_ScrollChanged;

            GoButton.Click += GoButton_Click;

            MenuItemPrefs.Click += MenuItemPrefs_Click;
        }

        private void MenuItemPrefs_Click(object sender, RoutedEventArgs e)
        {
            if (m_SettingsWindow == null)
            {
                m_SettingsWindow = new SettingsWindow();
                m_SettingsWindow.Owner = this;
            }

            m_SettingsWindow.Show();
        }

        private void LogViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // User scroll event : set or unset auto-scroll mode
            if (e.ExtentHeightChange == 0)
            {   // Content unchanged : user scroll event
                if (LogViewer.VerticalOffset == LogViewer.ScrollableHeight)
                {   // Scroll bar is in bottom
                    // Set auto-scroll mode
                    m_AutoScroll = true;
                }
                else
                {   // Scroll bar isn't in bottom
                    // Unset auto-scroll mode
                    m_AutoScroll = false;
                }
            }

            // Content scroll event : auto-scroll eventually
            if (m_AutoScroll && e.ExtentHeightChange != 0)
            {   // Content changed and auto-scroll mode set
                // Autoscroll
                LogViewer.ScrollToVerticalOffset(LogViewer.ExtentHeight);
            }
        }

        private void SourceMapPath_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Settings.LastSourceMap = SourceMapPath.Text;

            UpdateGoButtonEnabled();
        }

        private void TargetDirPath_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Settings.LastSourceDirectory = TargetDirectoryPath.Text;

            UpdateGoButtonEnabled();
        }

        private void OutputDirPath_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Settings.LastOutputDirectory = OutputDirectoryPath.Text;

            UpdateGoButtonEnabled();
        }

        private void UpdateGoButtonEnabled()
        {
            if (string.IsNullOrWhiteSpace(SourceMapPath.Text) ||
                string.IsNullOrWhiteSpace(TargetDirectoryPath.Text) ||
                string.IsNullOrWhiteSpace(OutputDirectoryPath.Text))
            {
                GoButton.IsEnabled = false;
            }
            else
            {
                GoButton.IsEnabled = true;
            }
        }

        private void SourceMapBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.FileName = SourceMapPath.Text;
            dialog.AddExtension = true;
            dialog.Filter = "Warcraft III TFT Map|*.w3x";
            if (dialog.ShowDialog() == true)
            {
                SourceMapPath.Text = dialog.FileName;
            }
        }

        private void TargetDirectoryBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.SelectedPath = TargetDirectoryPath.Text;

                System.Windows.Forms.DialogResult dialogResult = dialog.ShowDialog();

                if (dialogResult == System.Windows.Forms.DialogResult.OK)
                {
                    TargetDirectoryPath.Text = dialog.SelectedPath;
                }
            }
        }

        private void OutputDirectoryBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.SelectedPath = OutputDirectoryPath.Text;

                System.Windows.Forms.DialogResult dialogResult = dialog.ShowDialog();

                if (dialogResult == System.Windows.Forms.DialogResult.OK)
                {
                    OutputDirectoryPath.Text = dialog.SelectedPath;
                }
            }
        }

        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            if (m_DataTransferTask == null)
            {
                m_DataTransferTask = InitWorkflow();
            }
            else
            {
                m_WorkflowHandler.KillProcess = true;
            }
        }

        private void UpdateProgress(int currentStep, int totalSteps)
        {
            ProgressBar.Maximum = Math.Max(totalSteps, 1);

            ProgressBar.Value = Math.Min(currentStep, totalSteps);
        }

        private async Task InitWorkflow()
        {
            GoButton.Content = "Stop";
            UpdateProgress(0, 0);
            SoundManager.PlaySound(SoundType.Working);

            var context = SynchronizationContext.Current;
            Action<LogMessage> addLogs = (log) => context?.Send((state) => m_Logs.Add(log), null);
            Action<int, int> setProgress = (current, total) => context?.Send((state) => UpdateProgress(current, total), null);

            m_WorkflowHandler.Init(addLogs, setProgress);

            await Task.Run(() =>
            {
                try
                {
                    WarTransferArgs args = new WarTransferArgs();
                    args.SourceMap = Settings.LastSourceMap;
                    args.SourceDirectory = Settings.LastSourceDirectory;
                    args.OutputDirectory = Settings.LastOutputDirectory;
                    args.TransferRegions = Settings.TransferRegions;
                    args.TransferCameras = Settings.TransferCameras;
                    args.W3iFlagsToTransfer = Settings.TransferredFlags;

                    if (Settings.EnableVersioning)
                    {
                        args.VersionNumberRegex = Settings.VersionNumberRegex;
                    }

                    WarTransfer.WarTransfer transfer = new WarTransfer.WarTransfer();
                    transfer.ExecuteDataTransfer(m_WorkflowHandler, args);
                }
                catch (Exception ex)
                {
                    m_WorkflowHandler.ReportError(ex, true);
                }
            });

            if (!m_WorkflowHandler.KillProcess && m_WorkflowHandler.CurrentStep >= m_WorkflowHandler.TotalSteps)
            {
                SoundManager.PlaySound(SoundType.Done);
            }
            else
            {
                SoundManager.PlaySound(SoundType.Error);
            }

            UpdateProgress(0, 1);
            GoButton.Content = "Go";
            m_DataTransferTask = null;
        }
    }
}
