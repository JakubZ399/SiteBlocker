using System.Windows;

namespace SiteBlocker.UI
{
    public partial class PasswordDialog : Window
    {
        public string Password => PasswordBox.Password;
        
        public PasswordDialog(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
        }
        
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}