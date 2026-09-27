using AppCAT.Services;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace AppCAT.Views
{
    public partial class DotNetScanPage : Page
    {
        private ScanResult? _result;
        private CancellationTokenSource? _cts;

        public DotNetScanPage()
        {
            InitializeComponent();

            string desktop = Environment.GetFolderPath(
                Environment.SpecialFolder.Desktop);
            TxtReportPath.Text = Path.Combine(
                desktop, "AppCAT_Reports");

        }

        // Browsing 

        private void BrowseProject_Click(
            object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Solution or Project",
                Filter = "Solution (*.sln)|*.sln|" +
                         "Project (*.csproj)|*.csproj|" +
                         "All (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
                TxtProjectPath.Text = dlg.FileName;
        }

        private void BrowseReport_Click(
            object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Folder",
                CheckFileExists = false,
                FileName = "Select This Folder"
            };

            if (!string.IsNullOrEmpty(TxtReportPath.Text) &&
                Directory.Exists(TxtReportPath.Text))
                dlg.InitialDirectory = TxtReportPath.Text;

            if (dlg.ShowDialog() == true)
            {
                string? folder = Path.GetDirectoryName(
                    dlg.FileName);
                if (!string.IsNullOrEmpty(folder))
                    TxtReportPath.Text = folder;
            }
        }

     // Scanning

        private async void RunScan_Click(
            object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtProjectPath.Text)
                || !File.Exists(TxtProjectPath.Text.Trim()))
            {
                MessageBox.Show(
                    "Select a valid .sln or .csproj file.",
                    "Error", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtReportPath.Text))
            {
                MessageBox.Show(
                    "Select report save location.",
                    "Error", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string format = GetTag(CmbFormat, "APPMODJSON");
            string target = GetTag(CmbTarget, "Any");
            bool Non_Interactive = true;

            string command = ScanService.BuildCommand(
                TxtProjectPath.Text.Trim(),
                TxtReportPath.Text.Trim(),
                format,
                target,
                ChkCode.IsChecked == true,
                ChkBinaries.IsChecked == true,
                Non_Interactive);

            // ── UI: scanning state ──
            SetScanningState();


            // ── Cancellation token ──
            _cts = new CancellationTokenSource();

            Log($"Command:\n{command}\n");
            Log("Running in PowerShell...\n");

            try
            {
                _result = await ScanService.RunScanAsync(
                    TxtProjectPath.Text.Trim(),
                    TxtReportPath.Text.Trim(),
                    format,
                    target,
                    ChkCode.IsChecked == true,
                    ChkBinaries.IsChecked == true,
                    Non_Interactive,
                    _cts.Token);

                if (!string.IsNullOrWhiteSpace(_result.Output))
                    Log(_result.Output);

                if (!string.IsNullOrWhiteSpace(_result.Error))
                    Log($"STDERR:\n{_result.Error}");

                if (_result.Cancelled)
                {
                    TxtStatus.Text = "Scan cancelled";
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = 0;

                    Log("\nScan was cancelled by user");

                    MessageBox.Show(
                        "Scan was cancelled.",
                        "Cancelled",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else if (_result.Success)
                {
                    TxtStatus.Text = "Scan completed!";
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = 100;
                    BtnOpenReport.IsEnabled = true;

                    Log($"\nReport: {TxtReportPath.Text.Trim()}");

                    MessageBox.Show(
                        $".NET Project Scanned Successfully!",
                        "Success", MessageBoxButton.OK,
                        MessageBoxImage.Information); 
                }
                else
                {
                    TxtStatus.Text = "Scan failed";
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = 0;

                    Log("\nFailed");

                    MessageBox.Show(
                        $"Scan failed.\n\n{_result.Error}",
                        "Error", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (OperationCanceledException)
            {
                TxtStatus.Text = "Scan cancelled";
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Value = 0;

                Log("\nScan was cancelled by user");
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Error";
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Value = 0;

                Log($"\n{ex.Message}");

                MessageBox.Show(ex.Message, "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetCompletedState();


                _cts?.Dispose();
                _cts = null;
            }
        }

      

        private void CancelScan_Click(
            object sender, RoutedEventArgs e)
        {
            var answer = MessageBox.Show(
                "Are you sure you want to cancel the scan?",
                "Cancel Scan",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
                return;

            Log("\nCancelling scan...");
            TxtStatus.Text = "Cancelling...";

            _cts?.Cancel();
            ScanService.CancelScan();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.ShowInstallView();
            }
        }
        private void OpenReport_Click(
            object sender, RoutedEventArgs e)
        {
            if (_result != null)
                ScanService.OpenReport(_result.ReportPath);
        }

        

        //private void ScanAgain_Click(
        //    object sender, RoutedEventArgs e)
        //{
        //    SetInitialState();

        //}

        // ══════════════════════════════════════
        //  UI STATE HELPERS
        // ══════════════════════════════════════

        /// <summary>
        /// Initial state — only Run Scan visible
        /// </summary>
        //private void SetInitialState()
        //{
        //    BtnRun.Visibility = Visibility.Visible;
        //    BtnRun.IsEnabled = true;
        //    BtnCancel.Visibility = Visibility.Collapsed;
        //    BtnScanAgain.Visibility = Visibility.Collapsed;
        //    BtnOpenReport.IsEnabled = false;

        //    ProgressBar.Value = 0;
        //    ProgressBar.IsIndeterminate = false;
        //    TxtStatus.Text = "";
        //    TxtLog.Text = "";
        //    _result = null;
        //}

        /// <summary>
        /// Scanning state — Run hidden, Cancel shown
        /// </summary>
        private void SetScanningState()
        {
            BtnRun.Visibility = Visibility.Collapsed;
            BtnCancel.Visibility = Visibility.Visible;
            BtnScanAgain.Visibility = Visibility.Collapsed;
            BtnBack.Visibility = Visibility.Collapsed;
            BtnOpenReport.IsEnabled = false;

            ProgressBar.IsIndeterminate = true;
            TxtStatus.Text =
                "Scanning... This may take a few minutes";
            TxtLog.Text = "";
            _result = null;
        }

        /// <summary>
        /// Completed state — Run hidden, Scan Again shown
        /// </summary>
        private void SetCompletedState()
        {
            BtnRun.Visibility = Visibility.Collapsed;
            BtnCancel.Visibility = Visibility.Collapsed;
            BtnScanAgain.Visibility = Visibility.Visible;
            BtnBack.Visibility = Visibility.Visible;
        }


        private string GetTag(
            ComboBox combo, string defaultVal)
        {
            if (combo?.SelectedItem is ComboBoxItem item)
                return item.Tag?.ToString() ?? defaultVal;
            return defaultVal;
        }

        private void Log(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                TxtLog.Text += msg + "\n";
                LogScroll.ScrollToBottom();
            });
        }
    }
}