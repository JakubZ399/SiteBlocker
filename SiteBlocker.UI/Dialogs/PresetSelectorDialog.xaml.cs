using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SiteBlocker.Core;

namespace SiteBlocker.UI.Dialogs
{
    public partial class PresetSelectorDialog : Window
    {
        private HashSet<BlockList> _selectedPresets = new HashSet<BlockList>();
        
        public IReadOnlyCollection<BlockList> SelectedPresets => _selectedPresets;
        
        public PresetSelectorDialog(List<BlockList> presets)
        {
            InitializeComponent();
            
            PresetsListView.ItemsSource = presets;
        }
        
        private void PresetCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.Tag is BlockList preset)
            {
                _selectedPresets.Add(preset);
            }
        }
        
        private void PresetCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.Tag is BlockList preset)
            {
                _selectedPresets.Remove(preset);
            }
        }
        
        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}