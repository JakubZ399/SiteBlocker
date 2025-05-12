using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SiteBlocker.Core;
using SiteBlocker.UI.Dialogs;

namespace SiteBlocker.UI
{
    public partial class MainWindow : Window, IDisposable
    {
        private readonly SiteBlocker.Core.SiteBlocker _blocker = new SiteBlocker.Core.SiteBlocker();
        private BlockerConfig _config = null!; // Using null forgiving operator
        private readonly string _configPath;
        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private bool _isBlockingActive = false;
        private readonly ObservableCollection<string> _blockedSites = new ObservableCollection<string>();
        private readonly ObservableCollection<ScheduleItem> _scheduleItems = new ObservableCollection<ScheduleItem>();

        public MainWindow()
        {
            InitializeComponent(); // This will be generated from XAML

            // Path to configuration file
            _configPath = BlockerConfig.DefaultConfigPath;

            // Load configuration
            LoadConfig();
            
            // Initialize new controls for block lists and sessions
            BlockListsControl.Initialize(_config);
            SessionsControl.Initialize(_config, _blocker);
            
            InitializeScheduleComponents();

            // Set timer to update remaining time information
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;

            // Set data source for blocked sites list
            BlockedSitesListBox.ItemsSource = _blockedSites;

            // Check if the application is running with administrator privileges
            if (!AdminHelper.IsRunningAsAdmin())
            {
                MessageBoxResult result = MessageBox.Show(
                    "The application requires administrator privileges to modify the hosts file. " +
                    "Do you want to restart the application with administrator privileges?",
                    "Administrator privileges required",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    AdminHelper.RestartAsAdmin();
                    Application.Current.Shutdown();
                }
                else
                {
                    MessageBox.Show(
                        "The application may not work properly without administrator privileges.",
                        "Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }

        private void LoadConfig()
        {
            try
            {
                _config = BlockerConfig.LoadFromFile(_configPath);
                
                // Fill list of blocked sites
                _blockedSites.Clear();
                foreach (string site in _config.BlockedSites)
                {
                    _blockedSites.Add(site);
                }
                
                // Update status information
                UpdateStatusInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading configuration: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                // Create new configuration
                _config = new BlockerConfig();
            }
        }

        private void SaveConfig()
        {
            try
            {
                // Save configuration to file
                _config.SaveToFile(_configPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error saving configuration: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UpdateStatusInfo()
        {
            // Update blocking status information
            if (_config.IsEmergencyModeEnabled)
            {
                StatusTextBlock.Text = "Status: EMERGENCY MODE";
                TimeRemainingTextBlock.Text = "Time remaining: Blocking disabled";
                _isBlockingActive = false;
                _timer.Stop();
            }
            else if (!_config.IsActive)
            {
                StatusTextBlock.Text = "Status: Blocking disabled";
                TimeRemainingTextBlock.Text = "Time remaining: -";
                _isBlockingActive = false;
                _timer.Stop();
            }
            else
            {
                StatusTextBlock.Text = "Status: Blocking active";
                _isBlockingActive = true;
                
                // Update information about remaining time
                if (_config.BlockingStartTime.HasValue)
                {
                    TimeSpan elapsed = DateTime.Now - _config.BlockingStartTime.Value;
                    TimeSpan remaining = _config.MaxBlockingDuration - elapsed;
                    
                    if (remaining.TotalSeconds <= 0)
                    {
                        // Blocking time has elapsed - disable blocking
                        StopBlocking();
                    }
                    else
                    {
                        TimeRemainingTextBlock.Text = $"Time remaining: {remaining:mm\\:ss}";
                        if (!_timer.IsEnabled)
                        {
                            _timer.Start();
                        }
                    }
                }
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // Update status information
            UpdateStatusInfo();
        }

        private void AddSiteButton_Click(object sender, RoutedEventArgs e)
        {
            string site = SiteTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(site))
            {
                MessageBox.Show(
                    "Please enter a domain to block.",
                    "Empty field",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            
            // Add site to the list if it doesn't already exist
            if (!_blockedSites.Contains(site))
            {
                _blockedSites.Add(site);
                _config.BlockedSites.Add(site);
                SaveConfig();
                
                // If blocking is active, apply changes
                if (_isBlockingActive)
                {
                    _blocker.BlockSites(_config.BlockedSites);
                }
            }
            
            // Clear text field
            SiteTextBox.Text = "";
        }

        private void RemoveSiteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string site)
            {
                // Remove site from the list
                _blockedSites.Remove(site);
                _config.BlockedSites.Remove(site);
                SaveConfig();
                
                // If blocking is active, apply changes
                if (_isBlockingActive)
                {
                    if (_config.BlockedSites.Count > 0)
                    {
                        _blocker.BlockSites(_config.BlockedSites);
                    }
                    else
                    {
                        _blocker.UnblockSites();
                    }
                }
            }
        }

        private void StartBlockingButton_Click(object sender, RoutedEventArgs e)
        {
            if (_blockedSites.Count == 0)
            {
                MessageBox.Show(
                    "Please add at least one site to block.",
                    "Empty list",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            
            _blocker.UseHostsFile = HostsFileCheckBox.IsChecked ?? true;
            _blocker.UseFirewall = FirewallCheckBox.IsChecked ?? true;
            _blocker.UseWfp = WfpCheckBox.IsChecked ?? false;
            
            // Enable blocking
            _config.EnableBlocking();
            SaveConfig();
            
            // Apply block
            _blocker.BlockSites(_config.BlockedSites);
            
            // Update status information
            UpdateStatusInfo();
            
            MessageBox.Show(
                $"Blocking has been enabled for a maximum of {_config.MaxBlockingDuration.TotalMinutes} minutes.\n\n" +
                "Tips for effective blocking:\n" +
                "1. Close all browser windows\n" +
                "2. Clear browser cache\n" +
                "3. Restart browser\n\n" +
                "Some browsers (especially Chrome) may maintain their own DNS cache.",
                "Blocking enabled",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        
        private void InitializeScheduleComponents()
        {
            // Set data source for schedule list
            ScheduleListBox.ItemsSource = _scheduleItems;
    
            // Fill combo boxes for days and hours
            DayComboBox.ItemsSource = Enum.GetValues(typeof(DayOfWeek));
            DayComboBox.SelectedIndex = 0;
    
            // Fill hour and minute combo boxes
            for (int i = 0; i < 24; i++)
            {
                StartHourComboBox.Items.Add(i.ToString("00"));
                EndHourComboBox.Items.Add(i.ToString("00"));
            }
            StartHourComboBox.SelectedIndex = 9; // Default 9:00
            EndHourComboBox.SelectedIndex = 17; // Default 17:00
    
            for (int i = 0; i < 60; i += 15)
            {
                StartMinuteComboBox.Items.Add(i.ToString("00"));
                EndMinuteComboBox.Items.Add(i.ToString("00"));
            }
            StartMinuteComboBox.SelectedIndex = 0; // 00 minutes
            EndMinuteComboBox.SelectedIndex = 0; // 00 minutes
    
            // Fill schedule items list from configuration
            if (_config != null && _config.BlockingSchedule != null)
            {
                foreach (var item in _config.BlockingSchedule)
                {
                    _scheduleItems.Add(item);
                }
            }
        }

        private void StopBlockingButton_Click(object sender, RoutedEventArgs e)
        {
            if (!VerifyPasswordBeforeAction("stop blocking"))
            {
                MessageBox.Show("Incorrect password", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
    
            StopBlocking();
        }

        private void EmergencyButton_Click(object sender, RoutedEventArgs e)
        {
            // Enable emergency mode and remove blocks
            _config.EnableEmergencyMode();
            _blocker.UnblockSites();
            _blocker.EmergencyRestore(); 
            SaveConfig();
            
            // Update status information
            UpdateStatusInfo();
            
            MessageBox.Show(
                "EMERGENCY MODE has been activated. All blocks have been removed.",
                "Emergency mode",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void StopBlocking()
        {
            // Disable blocking
            _config.DisableBlocking();
            SaveConfig();
            
            // Remove block
            _blocker.UnblockSites();
            
            // Update status information
            UpdateStatusInfo();
            
            MessageBox.Show(
                "Blocking has been disabled.",
                "Blocking disabled",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        
        public void Dispose()
        {
            _blocker?.Dispose();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            // Stop timer
            _timer.Stop();
            
            // Ask whether to disable blocking when closing the application
            if (_isBlockingActive)
            {
                MessageBoxResult result = MessageBox.Show(
                    "Do you want to disable blocking before closing the application?",
                    "Blocking active",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    StopBlocking();
                }
            }
            
            Dispose();
        }
        
        private void AddScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get selected values
                DayOfWeek day = (DayOfWeek)DayComboBox.SelectedItem;
                int startHour = int.Parse(StartHourComboBox.SelectedItem.ToString());
                int startMinute = int.Parse(StartMinuteComboBox.SelectedItem.ToString());
                int endHour = int.Parse(EndHourComboBox.SelectedItem.ToString());
                int endMinute = int.Parse(EndMinuteComboBox.SelectedItem.ToString());
        
                // Check data validity
                if (endHour < startHour || (endHour == startHour && endMinute <= startMinute))
                {
                    MessageBox.Show(
                        "End time must be later than start time.",
                        "Invalid time range",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
        
                // Create new schedule item
                ScheduleItem item = new ScheduleItem
                {
                    Day = day,
                    StartTime = new TimeSpan(startHour, startMinute, 0),
                    EndTime = new TimeSpan(endHour, endMinute, 0),
                    IsEnabled = true
                };
        
                // Add to collection
                _scheduleItems.Add(item);
                _config.BlockingSchedule.Add(item);
                SaveConfig();

                MessageBox.Show("Schedule added successfully!", "Schedule", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error adding schedule: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RemoveScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ScheduleItem item)
            {
                _scheduleItems.Remove(item);
                _config.BlockingSchedule.Remove(item);
                SaveConfig();
            }
        }

        private void ScheduleCheckBox_Click(object sender, RoutedEventArgs e)
        {
            SaveConfig(); // Save state after changing schedule item activity
        }

        private void UseScheduleCheckBox_Click(object sender, RoutedEventArgs e)
        {
            // Set schedule usage flag in configuration
            SaveConfig();
        }
        
        private bool VerifyPasswordBeforeAction(string actionName)
        {
            if (string.IsNullOrEmpty(_config.PasswordHash))
                return true;
        
            // Show password dialog
            var passwordDialog = new PasswordDialog($"Enter password to {actionName}");
            if (passwordDialog.ShowDialog() == true)
            {
                return _config.VerifyPassword(passwordDialog.Password);
            }
            return false;
        }
        
        // Event handlers for the new controls
        private void SessionsControl_ConfigChanged(object sender, EventArgs e)
        {
            SaveConfig();
        }

        private void BlockListsControl_ConfigChanged(object sender, EventArgs e)
        {
            SaveConfig();
        }
    }
}