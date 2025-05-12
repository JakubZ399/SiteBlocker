using System.Windows;

namespace SiteBlocker.UI.Dialogs
{
    public partial class TextInputDialog : Window
    {
        public string InputText => InputTextBox.Text;
        
        public TextInputDialog(string title, string prompt, string defaultValue = "")
        {
            InitializeComponent();
            
            Title = title;
            PromptText.Text = prompt;
            InputTextBox.Text = defaultValue;
        }
        
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}