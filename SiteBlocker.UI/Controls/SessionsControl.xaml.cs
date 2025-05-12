using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SiteBlocker.Core;

namespace SiteBlocker.UI.Controls
{
    public partial class SessionsControl : UserControl
    {
        private BlockerConfig _config;
        private SiteBlocker.Core.SiteBlocker _blocker;
        private ObservableCollection<SessionViewModel> _sessions = new ObservableCollection<SessionViewModel>();
        private ObservableCollection<BlockList> _blockLists = new ObservableCollection<BlockList>();
        private HashSet<string> _selectedQuickBlockListIds = new HashSet<string>();
        private HashSet<string> _selectedScheduledBlockListIds = new HashSet<string>();
        private DispatcherTimer _refreshTimer = new DispatcherTimer();
        
        // Event to notify when changes are made that require saving
        public event EventHandler ConfigChanged;
        
        public SessionsControl()
        {
            InitializeComponent();
            
            // Set up data bindings
            SessionsItemsControl.ItemsSource = _sessions;
            
            // Set up refresh timer
            _refreshTimer.Interval = TimeSpan.FromSeconds(1);
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();
            
            // Initialize time combo boxes
            InitializeTimeComboBoxes();
        }
        
        private void InitializeTimeComboBoxes()
        {
            // Set up hours combo box (0-8 hours)
            List<ComboBoxItem> hoursList = new List<ComboBoxItem>();
            for (int i = 0; i <= 8; i++)
            {
                hoursList.Add(new ComboBoxItem { Display = i.ToString(), Value = i });
            }
            HoursComboBox.ItemsSource = hoursList;
            
            // Set up minutes combo box (0-59 minutes, 15-minute increments)
            List<ComboBoxItem> minutesList = new List<ComboBoxItem>();
            for (int i = 0; i < 60; i += 15)
            {
                minutesList.Add(new ComboBoxItem { Display = i.ToString(), Value = i });
            }
            MinutesComboBox.ItemsSource = minutesList;
            
            // Set up time of day combo boxes (24-hour format)
            List<ComboBoxItem> timeList = new List<ComboBoxItem>();
            for (int hour = 0; hour < 24; hour++)
            {
                for (int minute = 0; minute < 60; minute += 30)
                {
                    TimeSpan time = new TimeSpan(hour, minute, 0);
                    timeList.Add(new ComboBoxItem 
                    { 
                        Display = time.ToString(@"hh\:mm"), 
                        Value = time 
                    });
                }
            }
            StartTimeComboBox.ItemsSource = timeList;
            EndTimeComboBox.ItemsSource = new List<ComboBoxItem>(timeList);
        }
        
        public void Initialize(BlockerConfig config, SiteBlocker.Core.SiteBlocker blocker)
        {
            _config = config;
            _blocker = blocker;
            
            RefreshBlockLists();
            RefreshSessions();
        }
        
        private void RefreshBlockLists()
        {
            _blockLists.Clear();
            
            if (_config?.BlockLists == null)
                return;
                
            foreach (var list in _config.BlockLists.OrderBy(l => l.Name))
            {
                _blockLists.Add(list);
            }
            
            QuickBlockListsListView.ItemsSource = _blockLists;
            ScheduledBlockListsListView.ItemsSource = _blockLists;
        }
        
        private void RefreshSessions()
        {
            _sessions.Clear();
            
            if (_config?.BlockSessions == null)
                return;
                
            // Filter to only show active sessions
            var activeSessions = _config.BlockSessions
                .Where(s => s.IsActive && (!s.IsExpired || s.IsRecurring))
                .OrderBy(s => s.EndTime);
                
            foreach (var session in activeSessions)
            {
                _sessions.Add(new SessionViewModel(session, _config));
            }
        }
        
        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            // Update remaining time for sessions
            foreach (var sessionVM in _sessions)
            {
                sessionVM.UpdateRemainingTime();
                
                // Remove expired one-time sessions
                if (sessionVM.IsExpired && !sessionVM.IsRecurring)
                {
                    sessionVM.Session.IsActive = false;
                    OnConfigChanged();
                }
            }
            
            // Remove expired sessions from the UI
            var expiredSessions = _sessions.Where(s => s.IsExpired && !s.IsRecurring).ToList();
            foreach (var expiredSession in expiredSessions)
            {
                _sessions.Remove(expiredSession);
            }
            
            // Apply blocking for all active sessions
            ApplyBlockingForActiveSessions();
        }
        
        private void ApplyBlockingForActiveSessions()
        {
            // Get all active sessions that should be active now
            var activeSessionsNow = _config.BlockSessions
                .Where(s => s.IsActive && s.ShouldBeActiveNow())
                .ToList();
                
            if (activeSessionsNow.Count > 0)
            {
                // Collect all sites that should be blocked
                HashSet<string> sitesToBlock = new HashSet<string>();
                
                foreach (var session in activeSessionsNow)
                {
                    foreach (string listId in session.BlockListIds)
                    {
                        var list = _config.GetBlockListById(listId);
                        if (list != null)
                        {
                            foreach (string site in list.Sites)
                            {
                                sitesToBlock.Add(site);
                            }
                        }
                    }
                }
                
                // Apply blocking
                if (sitesToBlock.Count > 0)
                {
                    _blocker.BlockSites(sitesToBlock.ToList());
                }
            }
            else
            {
                // No active sessions - remove all blocks
                _blocker.UnblockSites();
            }
        }
        
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshSessions();
        }
        
        private void BlockListCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.Tag is BlockList list)
            {
                if (SessionTabControl.SelectedIndex == 0) // Quick session tab
                {
                    _selectedQuickBlockListIds.Add(list.Id);
                }
                else // Scheduled session tab
                {
                    _selectedScheduledBlockListIds.Add(list.Id);
                }
            }
        }
        
        private void BlockListCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.Tag is BlockList list)
            {
                if (SessionTabControl.SelectedIndex == 0) // Quick session tab
                {
                    _selectedQuickBlockListIds.Remove(list.Id);
                }
                else // Scheduled session tab
                {
                    _selectedScheduledBlockListIds.Remove(list.Id);
                }
            }
        }
        
        private void StartSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (SessionTabControl.SelectedIndex == 0) // Quick session tab
            {
                StartQuickSession();
            }
            else // Scheduled session tab
            {
                StartScheduledSession();
            }
        }
        
        private void StartQuickSession()
        {
            if (_selectedQuickBlockListIds.Count == 0)
            {
                MessageBox.Show("Please select at least one block list.", "No Lists Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Get session name
            string name = QuickSessionNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Quick Session";
            }
            
            // Get duration
            int hours = (int)(HoursComboBox.SelectedItem as ComboBoxItem)?.Value;
            int minutes = (int)(MinutesComboBox.SelectedItem as ComboBoxItem)?.Value;
            TimeSpan duration = new TimeSpan(hours, minutes, 0);
            
            if (duration <= TimeSpan.Zero)
            {
                MessageBox.Show("Please set a duration greater than zero.", "Invalid Duration", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Create session
            BlockSession session = new BlockSession
            {
                Name = name,
                BlockListIds = new List<string>(_selectedQuickBlockListIds),
                StartTime = DateTime.Now,
                Duration = duration,
                IsActive = true,
                IsRecurring = false
            };
            
            // Add to config
            _config.BlockSessions.Add(session);
            
            // Apply blocking
            ApplyBlockingForActiveSessions();
            
            // Save changes
            OnConfigChanged();
            
            // Refresh UI
            RefreshSessions();
            
            // Reset selection
            ResetQuickBlockListSelection();
            
            MessageBox.Show($"Session '{name}' started for {hours} hours and {minutes} minutes.", "Session Started", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void StartScheduledSession()
        {
            if (_selectedScheduledBlockListIds.Count == 0)
            {
                MessageBox.Show("Please select at least one block list.", "No Lists Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Check if any days are selected
            List<DayOfWeek> selectedDays = new List<DayOfWeek>();
            if (MondayCheckBox.IsChecked == true) selectedDays.Add(DayOfWeek.Monday);
            if (TuesdayCheckBox.IsChecked == true) selectedDays.Add(DayOfWeek.Tuesday);
            if (WednesdayCheckBox.IsChecked == true) selectedDays.Add(DayOfWeek.Wednesday);
            if (ThursdayCheckBox.IsChecked == true) selectedDays.Add(DayOfWeek.Thursday);
            if (FridayCheckBox.IsChecked == true) selectedDays.Add(DayOfWeek.Friday);
            if (SaturdayCheckBox.IsChecked == true) selectedDays.Add(DayOfWeek.Saturday);
            if (SundayCheckBox.IsChecked == true) selectedDays.Add(DayOfWeek.Sunday);
            
            if (selectedDays.Count == 0)
            {
                MessageBox.Show("Please select at least one day of the week.", "No Days Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Get session name
            string name = ScheduledSessionNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Scheduled Session";
            }
            
            // Get time range
            TimeSpan startTime = (TimeSpan)(StartTimeComboBox.SelectedItem as ComboBoxItem)?.Value;
            TimeSpan endTime = (TimeSpan)(EndTimeComboBox.SelectedItem as ComboBoxItem)?.Value;
            
            if (startTime >= endTime)
            {
                MessageBox.Show("End time must be later than start time.", "Invalid Time Range", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Create session
            BlockSession session = new BlockSession
            {
                Name = name,
                BlockListIds = new List<string>(_selectedScheduledBlockListIds),
                IsActive = true,
                IsRecurring = true,
                RecurringDays = selectedDays,
                StartTimeOfDay = startTime,
                EndTimeOfDay = endTime
            };
            
            // Add to config
            _config.BlockSessions.Add(session);
            
            // Apply blocking
            ApplyBlockingForActiveSessions();
            
            // Save changes
            OnConfigChanged();
            
            // Refresh UI
            RefreshSessions();
            
            // Reset selection
            ResetScheduledBlockListSelection();
            
            // Reset days
            MondayCheckBox.IsChecked = false;
            TuesdayCheckBox.IsChecked = false;
            WednesdayCheckBox.IsChecked = false;
            ThursdayCheckBox.IsChecked = false;
            FridayCheckBox.IsChecked = false;
            SaturdayCheckBox.IsChecked = false;
            SundayCheckBox.IsChecked = false;
            
            MessageBox.Show($"Scheduled session '{name}' has been created and is now active.", "Session Created", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void ResetQuickBlockListSelection()
        {
            _selectedQuickBlockListIds.Clear();
            
            foreach (var item in QuickBlockListsListView.Items)
            {
                var container = QuickBlockListsListView.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                if (container != null)
                {
                    var checkBox = FindVisualChild<CheckBox>(container);
                    if (checkBox != null)
                    {
                        checkBox.IsChecked = false;
                    }
                }
            }
        }
        
        private void ResetScheduledBlockListSelection()
        {
            _selectedScheduledBlockListIds.Clear();
            
            foreach (var item in ScheduledBlockListsListView.Items)
            {
                var container = ScheduledBlockListsListView.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                if (container != null)
                {
                    var checkBox = FindVisualChild<CheckBox>(container);
                    if (checkBox != null)
                    {
                        checkBox.IsChecked = false;
                    }
                }
            }
        }
        
        private void StopSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SessionViewModel sessionVM)
            {
                var result = MessageBox.Show($"Are you sure you want to stop the session '{sessionVM.Name}'?", 
                                           "Stop Session", 
                                           MessageBoxButton.YesNo, 
                                           MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    // Deactivate session
                    sessionVM.Session.IsActive = false;
                    
                    // Apply blocking changes
                    ApplyBlockingForActiveSessions();
                    
                    // Save changes
                    OnConfigChanged();
                    
                    // Refresh UI
                    RefreshSessions();
                }
            }
        }
        
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T typedChild)
                {
                    return typedChild;
                }
                
                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            
            return null;
        }
        
        private void OnConfigChanged()
        {
            ConfigChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    
    // Helper classes
    
    public class ComboBoxItem
    {
        public string Display { get; set; }
        public object Value { get; set; }
    }
    
    public class SessionViewModel : INotifyPropertyChanged
    {
        private readonly BlockerConfig _config;
        
        public BlockSession Session { get; }
        
        public string Name => Session.Name;
        
        public bool IsRecurring => Session.IsRecurring;
        
        public string RecurringDaysDisplay => Session.RecurringDaysDisplay;
        
        public string StartTimeOfDayDisplay => Session.StartTimeOfDay.ToString(@"hh\:mm");
        
        public string EndTimeOfDayDisplay => Session.EndTimeOfDay.ToString(@"hh\:mm");
        
        private string _remainingTimeDisplay;
        public string RemainingTimeDisplay
        {
            get => _remainingTimeDisplay;
            set
            {
                if (_remainingTimeDisplay != value)
                {
                    _remainingTimeDisplay = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RemainingTimeDisplay)));
                }
            }
        }
        
        public bool IsExpired => Session.IsExpired;
        
        public ObservableCollection<BlockList> UsedBlockLists { get; } = new ObservableCollection<BlockList>();
        
        public SessionViewModel(BlockSession session, BlockerConfig config)
        {
            Session = session;
            _config = config;
            
            UpdateRemainingTime();
            LoadUsedBlockLists();
        }
        
        public void UpdateRemainingTime()
        {
            if (!IsRecurring)
            {
                TimeSpan remaining = Session.RemainingTime;
                if (remaining.TotalHours >= 1)
                {
                    RemainingTimeDisplay = $"{(int)remaining.TotalHours}h {remaining.Minutes}m {remaining.Seconds}s";
                }
                else
                {
                    RemainingTimeDisplay = $"{remaining.Minutes}m {remaining.Seconds}s";
                }
            }
        }
        
        private void LoadUsedBlockLists()
        {
            UsedBlockLists.Clear();
            
            foreach (string listId in Session.BlockListIds)
            {
                BlockList list = _config.GetBlockListById(listId);
                if (list != null)
                {
                    UsedBlockLists.Add(list);
                }
            }
        }
        
        public event PropertyChangedEventHandler PropertyChanged;
    }
}