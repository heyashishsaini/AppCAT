using AppCAT.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AppCAT.Views
{
    public partial class MainWindow : Window
    {
        private InstallationType _selectedType;
        private CancellationTokenSource? _cts;

        public MainWindow()
        {
            InitializeComponent();
        }

        // ─── Card Hover Effects ───────────────────────────────────────────────

        private void DotNetCard_MouseEnter(object sender, MouseEventArgs e)
            => DotNetCard.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));

        private void DotNetCard_MouseLeave(object sender, MouseEventArgs e)
            => DotNetCard.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0xE8, 0xFF));

        private void JavaCard_MouseEnter(object sender, MouseEventArgs e)
            => JavaCard.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));

        private void JavaCard_MouseLeave(object sender, MouseEventArgs e)
            => JavaCard.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0xE8, 0xFF));

        // ─── Card Click Handlers ─────────────────────────────────────────────

        private async void DotNetCard_Click(object sender, MouseButtonEventArgs e)
        {
            _selectedType = InstallationType.DotNet;
            await StartInstallationFlowAsync();
        }

        private async void JavaCard_Click(object sender, MouseButtonEventArgs e)
        {
            _selectedType = InstallationType.Java;
            await StartInstallationFlowAsync();
        }

        // ─── Main Installation Flow ──────────────────────────────────────────

        private async Task StartInstallationFlowAsync()
        {
            // Step 1: Internet check
            AppendLog("Checking internet connection...");
            ShowInstallingScreen();

            bool hasInternet = await InternetService.IsInternetAvailableAsync();

            if (!hasInternet)
            {
                // Go back to welcome and show popup
                ShowWelcomeScreen();
                ShowNoInternetPopup();
                return;
            }

            AppendLog("Internet connection detected.\n");

            // Step 2: Run installation
            await RunInstallationAsync();
        }

        private async Task RunInstallationAsync()
        {
            _cts = new CancellationTokenSource();
            CancelButton.Visibility = Visibility.Visible;
            ActionButtons.Visibility = Visibility.Collapsed;

            // Update title based on type
            if (_selectedType == InstallationType.DotNet)
            {
                
                InstallTitleText.Text = "Installing .NET AppCAT...";
                InstallSubtitleText.Text = "Installing via dotnet tool globally";
            }
            else
            {
                
                InstallTitleText.Text = "Installing Java AppCAT...";
                InstallSubtitleText.Text = "Downloading and extracting AppCAT for Java";
            }

            var progress = new Progress<InstallationProgress>(p =>
            {
                InstallProgressBar.Value = p.Percentage;
                ProgressPercentText.Text = $"{p.Percentage}%";
                AppendLog(p.Message, p.IsError);
            });

            try
            {
                if (_selectedType == InstallationType.DotNet)
                    await InstallationService.InstallDotNetAppcatAsync(progress, _cts.Token);
                else
                    await InstallationService.InstallJavaAppcatAsync(progress, _cts.Token);

                // Success
                OnInstallationSuccess();
            }
            catch (OperationCanceledException)
            {
                AppendLog("\nInstallation cancelled by user.", isError: true);
                OnInstallationCancelled();
            }
            catch (Exception ex)
            {
                AppendLog($"\nError: {ex.Message}", isError: true);
                OnInstallationFailed();
            }
            finally
            {
                CancelButton.Visibility = Visibility.Collapsed;
            }
        }

        // ─── Outcome Handlers ────────────────────────────────────────────────

        private void OnInstallationSuccess()
        {
            
            InstallTitleText.Text = "Installation Complete!";
            InstallTitleText.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 16));
            InstallSubtitleText.Text = _selectedType == InstallationType.DotNet
                ? "AppCAT for .NET is ready."
                : "AppCAT for Java is ready.";

            InstallProgressBar.Value = 100;
            ProgressPercentText.Text = "100%";
            InstallProgressBar.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 16));

            ActionButtons.Visibility = Visibility.Visible;
            ScanAppButton.Content = "Start Scanning";

            AppendLog("\n Click 'Start Scanning' to analyze your project!");
        }

        private void OnInstallationFailed()
        {
            InstallTitleText.Text = "Installation Failed";
            InstallTitleText.Foreground = new SolidColorBrush(Color.FromRgb(209, 52, 56));
            InstallSubtitleText.Text = "An error occurred. Check the log below.";
            ActionButtons.Visibility = Visibility.Visible;
            ScanAppButton.Content = "Close";
        }

        private void OnInstallationCancelled()
        {
            InstallTitleText.Text = "Installation Cancelled";
            InstallTitleText.Foreground = new SolidColorBrush(Color.FromRgb(255, 140, 0));
            ActionButtons.Visibility = Visibility.Visible;
            ScanAppButton.Content = "Close";
        }

        // ─── No Internet Popup ───────────────────────────────────────────────

        private async void ShowNoInternetPopup()
        {
            var popup = new NoInternetWindow { Owner = this };
            popup.ShowDialog();

            if (popup.RetryRequested)
            {
                // Retry the whole flow
                await StartInstallationFlowAsync();
            }
        }

        // ─── Screen Switchers ────────────────────────────────────────────────

        private void ShowInstallingScreen()
        {
            WelcomeScreen.Visibility = Visibility.Collapsed;
            InstallingScreen.Visibility = Visibility.Visible;
            LogText.Text = "";
            InstallProgressBar.Value = 0;
            ProgressPercentText.Text = "0%";
            InstallTitleText.Foreground = FindResource("TextPrimaryBrush") as SolidColorBrush;
            InstallProgressBar.Foreground = FindResource("PrimaryBrush") as SolidColorBrush;
        }

        private void ShowWelcomeScreen()
        {
            WelcomeScreen.Visibility = Visibility.Visible;
            InstallingScreen.Visibility = Visibility.Collapsed;
        }

        public void ShowInstallView()
        {
            MainFrame.Visibility = Visibility.Collapsed;
            InstallingScreen.Visibility = Visibility.Visible;

            // restore all hidden controls
        }

        // ─── Button Handlers ─────────────────────────────────────────────────

        private void BackButton_Click(object sender, RoutedEventArgs e)
            => ShowWelcomeScreen();

        private void ScanAppButton_Click(
    object sender, RoutedEventArgs e)
        {
            if (InstallTitleText.Text == "Installation Complete!")
            {
                // Hide all MainWindow content, show Frame
                MainFrame.Visibility = Visibility.Visible;

                // Hide everything else
                foreach (UIElement child in ((Grid)this.Content).Children)
                {
                    if (child != MainFrame)
                        child.Visibility = Visibility.Collapsed;
                }

                if (_selectedType == InstallationType.DotNet)
                {
                    MainFrame.Navigate(new DotNetScanPage());
                }
                else if (_selectedType == InstallationType.Java)
                {
                    MainFrame.Navigate(new JavaScanPage());
                }
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
            => _cts?.Cancel();

        // ─── Logging Helper ──────────────────────────────────────────────────

        private void AppendLog(string message, bool isError = false)
        {
            Dispatcher.Invoke(() =>
            {
                var prefix = isError ? "[ERROR] " : "[INFO]  ";
                LogText.Text += $"{prefix}{message}\n";
                LogScrollViewer.ScrollToBottom();
            });
        }
    }
}