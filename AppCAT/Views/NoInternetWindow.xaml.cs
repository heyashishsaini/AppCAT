using System.Windows;

namespace AppCAT.Views
{
    public partial class NoInternetWindow : Window
    {
        public bool RetryRequested { get; private set; } = false;

        public NoInternetWindow()
        {
            InitializeComponent();
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            RetryRequested = true;
            this.Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            RetryRequested = false;
            this.Close();
        }
    }
}