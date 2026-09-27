using AppCAT.Services;
using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace AppCAT.Views
{
    public partial class JavaScanPage : Page
    {
        private JavaScanResult? _result;
        private CancellationTokenSource? _cts;

        public JavaScanPage()
        {
            InitializeComponent();

            string desktop = Environment.GetFolderPath(
                Environment.SpecialFolder.Desktop);
            TxtReportPath.Text = Path.Combine(
                desktop, "AppCAT_Java_Reports");
        }

        // ══════════════════════════════════════
        //  BROWSING
        // ══════════════════════════════════════

        private void BrowseProject_Click(
            object sender, RoutedEventArgs e)
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select Java Project Folder",
                ShowNewFolderButton = false,
                UseDescriptionForTitle = true
            };

            if (!string.IsNullOrEmpty(TxtProjectPath.Text) &&
                Directory.Exists(TxtProjectPath.Text))
            {
                dlg.SelectedPath = TxtProjectPath.Text;
            }

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TxtProjectPath.Text = dlg.SelectedPath;
            }
        }

        private void BrowseReport_Click(
            object sender, RoutedEventArgs e)
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select Report Save Location",
                ShowNewFolderButton = true,
                UseDescriptionForTitle = true
            };

            if (!string.IsNullOrEmpty(TxtReportPath.Text) &&
                Directory.Exists(TxtReportPath.Text))
            {
                dlg.SelectedPath = TxtReportPath.Text;
            }

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TxtReportPath.Text = dlg.SelectedPath;
            }
        }

        // ══════════════════════════════════════
        //  SCANNING
        // ══════════════════════════════════════

        private async void RunScan_Click(
            object sender, RoutedEventArgs e)
        {
            // Validate input path
            if (string.IsNullOrWhiteSpace(TxtProjectPath.Text)
                || !Directory.Exists(TxtProjectPath.Text.Trim()))
            {
                System.Windows.MessageBox.Show(
                    "Select a valid Java project folder.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Validate output path
            if (string.IsNullOrWhiteSpace(TxtReportPath.Text))
            {
                System.Windows.MessageBox.Show(
                    "Select report save location.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string format = GetTag(CmbFormat, "");
            string target = GetTag(CmbTarget, "azure-appservice");
            string capability = GetTag(CmbCapability, "");
            string os = GetTag(CmbOS, "windows");
            bool overwrite = ChkOverwrite.IsChecked == true;

            string command = JavaScanService.BuildCommand(
                TxtProjectPath.Text.Trim(),
                TxtReportPath.Text.Trim(),
                format,
                target,
                capability,
                os,
                overwrite);

            // ── UI: scanning state ──
            SetScanningState();

            // ── Cancellation token ──
            _cts = new CancellationTokenSource();

            Log($"Command:\n{command}\n");
            Log("Running Java AppCAT scan...\n");

            try
            {
                _result = await JavaScanService.RunScanAsync(
                    TxtProjectPath.Text.Trim(),
                    TxtReportPath.Text.Trim(),
                    format,
                    target,
                    capability,
                    os,
                    overwrite,
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

                    System.Windows.MessageBox.Show(
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

                    Log($"\nReport: {_result.ReportPath}");

                    System.Windows.MessageBox.Show(
                        "Java Project Scanned Successfully!",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    TxtStatus.Text = "Scan failed";
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = 0;

                    Log("\nFailed");

                    System.Windows.MessageBox.Show(
                        $"Scan failed.\n\n{_result.Error}",
                        "Error",
                        MessageBoxButton.OK,
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

                System.Windows.MessageBox.Show(
                    ex.Message,
                    "Error",
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
            var answer = System.Windows.MessageBox.Show(
                "Are you sure you want to cancel the scan?",
                "Cancel Scan",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
                return;

            Log("\nCancelling scan...");
            TxtStatus.Text = "Cancelling...";

            _cts?.Cancel();
            JavaScanService.CancelScan();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.ShowInstallView();
            }
        }
        private void OpenReport_Click(
            object sender, RoutedEventArgs e)
        {
            if (_result != null)
                JavaScanService.OpenReport(_result.ReportPath);
        }

        // ══════════════════════════════════════
        //  UI STATE HELPERS
        // ══════════════════════════════════════

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

        private void SetCompletedState()
        {
            BtnRun.Visibility = Visibility.Collapsed;
            BtnCancel.Visibility = Visibility.Collapsed;
            BtnScanAgain.Visibility = Visibility.Visible;
            BtnBack.Visibility = Visibility.Visible;
        }

        private string GetTag(
            System.Windows.Controls.ComboBox combo, string defaultVal)
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