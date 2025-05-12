using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SiteBlocker.Core;
using SiteBlocker.UI.Dialogs;

namespace SiteBlocker.UI.Controls
{
    public partial class BlockListsControl : UserControl
    {
        private BlockerConfig _config;
        private ObservableCollection<BlockList> _blockLists = new ObservableCollection<BlockList>();
        private ObservableCollection<string> _currentSites = new ObservableCollection<string>();
        private BlockList _selectedList;
        private bool _hasUnsavedChanges = false;
        
        // Property for enabling/disabling site editing
        public bool IsEditEnabled => _selectedList != null && !_selectedList.IsBuiltIn;
        
        // Event to notify when changes are made that require saving
        public event EventHandler ConfigChanged;
        
        public BlockListsControl()
        {
            InitializeComponent();
            
            // Set up data bindings
            BlockListsListView.ItemsSource = _blockLists;
            SitesListView.ItemsSource = _currentSites;
        }
        
        public void Initialize(BlockerConfig config)
        {
            _config = config;
            
            // Create built-in categories if needed
            _config.AddBuiltInCategories();
            
            RefreshBlockLists();
        }
        
        private void RefreshBlockLists()
        {
            _blockLists.Clear();
            
            if (_config?.BlockLists == null)
                return;
                
            foreach (var list in _config.BlockLists.OrderBy(l => l.IsBuiltIn).ThenBy(l => l.Name))
            {
                _blockLists.Add(list);
            }
        }
        
        private void BlockListsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Check for unsaved changes
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show("You have unsaved changes. Save them now?", 
                                            "Unsaved Changes", 
                                            MessageBoxButton.YesNoCancel, 
                                            MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Cancel)
                {
                    // Revert selection change
                    BlockListsListView.SelectedItem = _selectedList;
                    return;
                }
                else if (result == MessageBoxResult.Yes)
                {
                    SaveCurrentList();
                }
            }
            
            _selectedList = BlockListsListView.SelectedItem as BlockList;
            _currentSites.Clear();
            
            if (_selectedList != null)
            {
                SelectedListName.Text = _selectedList.Name;
                
                foreach (var site in _selectedList.Sites)
                {
                    _currentSites.Add(site);
                }
                
                SaveListButton.IsEnabled = !_selectedList.IsBuiltIn;
            }
            else
            {
                SelectedListName.Text = "Select a list";
                SaveListButton.IsEnabled = false;
            }
            
            _hasUnsavedChanges = false;
        }
        
        private void AddListButton_Click(object sender, RoutedEventArgs e)
        {
            // Show dialog to create a new list
            var dialog = new TextInputDialog("Create Block List", "Enter a name for the new block list:", "New Block List");
            dialog.Owner = Window.GetWindow(this);
            
            if (dialog.ShowDialog() == true)
            {
                string name = dialog.InputText.Trim();
                
                if (string.IsNullOrWhiteSpace(name))
                {
                    MessageBox.Show("Please enter a valid name for the block list.", "Invalid Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Create new block list
                BlockList newList = new BlockList { Name = name };
                _config.BlockLists.Add(newList);
                
                // Refresh UI
                RefreshBlockLists();
                
                // Select the new list
                BlockListsListView.SelectedItem = _blockLists.FirstOrDefault(l => l.Id == newList.Id);
                
                // Save changes
                OnConfigChanged();
            }
        }
        
        private void DeleteListButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is BlockList list)
            {
                // Don't allow deleting built-in lists
                if (list.IsBuiltIn)
                {
                    MessageBox.Show("Built-in categories cannot be deleted.", "Delete List", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                var result = MessageBox.Show($"Are you sure you want to delete the list '{list.Name}'?", 
                                           "Delete List", 
                                           MessageBoxButton.YesNo, 
                                           MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    // Remove from sessions
                    foreach (var session in _config.BlockSessions)
                    {
                        session.BlockListIds.Remove(list.Id);
                    }
                    
                    // Remove the list
                    _config.BlockLists.Remove(list);
                    RefreshBlockLists();
                    
                    // Clear selection if the deleted list was selected
                    if (_selectedList == list)
                    {
                        _selectedList = null;
                        _currentSites.Clear();
                        SelectedListName.Text = "Select a list";
                        SaveListButton.IsEnabled = false;
                    }
                    
                    // Save changes
                    OnConfigChanged();
                }
            }
        }
        
        private void AddSiteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedList == null || _selectedList.IsBuiltIn)
                return;
                
            string site = AddSiteTextBox.Text.Trim();
            
            if (string.IsNullOrWhiteSpace(site))
            {
                MessageBox.Show("Please enter a domain to block.", "Empty Field", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Clean up URL format
            site = CleanDomainName(site);
            
            // Add site if it doesn't already exist
            if (!_currentSites.Contains(site))
            {
                _currentSites.Add(site);
                _hasUnsavedChanges = true;
            }
            
            // Clear input field
            AddSiteTextBox.Text = "";
        }
        
        private void RemoveSiteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedList == null || _selectedList.IsBuiltIn)
                return;
                
            if (sender is Button button && button.Tag is string site)
            {
                _currentSites.Remove(site);
                _hasUnsavedChanges = true;
            }
        }
        
        private void SaveListButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentList();
        }
        
        private void SaveCurrentList()
        {
            if (_selectedList == null || _selectedList.IsBuiltIn)
                return;
                
            // Update the list with current sites
            _selectedList.Sites.Clear();
            foreach (string site in _currentSites)
            {
                _selectedList.Sites.Add(site);
            }
            
            _selectedList.ModifiedDate = DateTime.Now;
            _hasUnsavedChanges = false;
            
            // Save changes to config
            OnConfigChanged();
            
            MessageBox.Show($"Block list '{_selectedList.Name}' has been saved.", "List Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void ImportPresetsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedList == null || _selectedList.IsBuiltIn)
                return;
                
            // Create dialog with checkboxes for each built-in category
            var builtInLists = _config.BlockLists.Where(l => l.IsBuiltIn).ToList();
            if (builtInLists.Count == 0)
            {
                MessageBox.Show("No built-in categories available.", "Import Presets", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            var dialog = new PresetSelectorDialog(builtInLists);
            dialog.Owner = Window.GetWindow(this);
            
            if (dialog.ShowDialog() == true)
            {
                foreach (var preset in dialog.SelectedPresets)
                {
                    // Add sites from selected preset to current list
                    foreach (string site in preset.Sites)
                    {
                        if (!_currentSites.Contains(site))
                        {
                            _currentSites.Add(site);
                        }
                    }
                }
                
                _hasUnsavedChanges = true;
                MessageBox.Show("Preset sites were added to the list. Don't forget to save your changes.", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        private string CleanDomainName(string domain)
        {
            string cleanDomain = domain
                .Replace("http://", "")
                .Replace("https://", "")
                .TrimEnd('/');
                
            int pathIndex = cleanDomain.IndexOf('/');
            if (pathIndex > 0)
                cleanDomain = cleanDomain.Substring(0, pathIndex);
            
            int queryIndex = cleanDomain.IndexOf('?');
            if (queryIndex > 0)
                cleanDomain = cleanDomain.Substring(0, queryIndex);
                
            return cleanDomain;
        }
        
        private void OnConfigChanged()
        {
            ConfigChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}