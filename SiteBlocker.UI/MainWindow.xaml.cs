using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SiteBlocker.Core;

namespace SiteBlocker.UI
{
    public partial class MainWindow : Window, IDisposable
    {
        private readonly SiteBlocker.Core.SiteBlocker _blocker = new SiteBlocker.Core.SiteBlocker();
        private BlockerConfig _config;
        private readonly string _configPath;
        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private bool _isBlockingActive = false;
        private readonly ObservableCollection<string> _blockedSites = new ObservableCollection<string>();
        private ObservableCollection<ScheduleItem> _scheduleItems = new ObservableCollection<ScheduleItem>();

        public MainWindow()
        {
            InitializeComponent();
            InitializeScheduleComponents();

            // Ścieżka do pliku konfiguracyjnego
            _configPath = BlockerConfig.DefaultConfigPath;

            // Załaduj konfigurację
            LoadConfig();

            // Ustaw timer do aktualizacji informacji o pozostałym czasie
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;

            // Ustaw źródło danych dla listy zablokowanych stron
            BlockedSitesListBox.ItemsSource = _blockedSites;

            // Sprawdź, czy aplikacja działa z uprawnieniami administratora
            if (!AdminHelper.IsRunningAsAdmin())
            {
                MessageBoxResult result = MessageBox.Show(
                    "Aplikacja wymaga uprawnień administratora do modyfikacji pliku hosts. " +
                    "Czy chcesz uruchomić aplikację ponownie z uprawnieniami administratora?",
                    "Wymagane uprawnienia administratora",
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
                        "Aplikacja może nie działać poprawnie bez uprawnień administratora.",
                        "Ostrzeżenie",
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
                
                // Wypełnij listę zablokowanych stron
                _blockedSites.Clear();
                foreach (string site in _config.BlockedSites)
                {
                    _blockedSites.Add(site);
                }
                
                // Zaktualizuj informacje o statusie
                UpdateStatusInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Błąd podczas ładowania konfiguracji: {ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                // Utwórz nową konfigurację
                _config = new BlockerConfig();
            }
        }

        private void SaveConfig()
        {
            try
            {
                // Zapisz konfigurację do pliku
                _config.SaveToFile(_configPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Błąd podczas zapisywania konfiguracji: {ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UpdateStatusInfo()
        {
            // Zaktualizuj informację o statusie blokowania
            if (_config.IsEmergencyModeEnabled)
            {
                StatusTextBlock.Text = "Status: TRYB AWARYJNY";
                TimeRemainingTextBlock.Text = "Czas pozostały: Blokada wyłączona";
                _isBlockingActive = false;
                _timer.Stop();
            }
            else if (!_config.IsActive)
            {
                StatusTextBlock.Text = "Status: Blokowanie wyłączone";
                TimeRemainingTextBlock.Text = "Czas pozostały: -";
                _isBlockingActive = false;
                _timer.Stop();
            }
            else
            {
                StatusTextBlock.Text = "Status: Blokowanie aktywne";
                _isBlockingActive = true;
                
                // Zaktualizuj informację o pozostałym czasie
                if (_config.BlockingStartTime.HasValue)
                {
                    TimeSpan elapsed = DateTime.Now - _config.BlockingStartTime.Value;
                    TimeSpan remaining = _config.MaxBlockingDuration - elapsed;
                    
                    if (remaining.TotalSeconds <= 0)
                    {
                        // Czas blokady minął - wyłącz blokowanie
                        StopBlocking();
                    }
                    else
                    {
                        TimeRemainingTextBlock.Text = $"Czas pozostały: {remaining:mm\\:ss}";
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
            // Aktualizuj informację o statusie
            UpdateStatusInfo();
        }

        private void AddSiteButton_Click(object sender, RoutedEventArgs e)
        {
            string site = SiteTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(site))
            {
                MessageBox.Show(
                    "Proszę wpisać domenę do zablokowania.",
                    "Puste pole",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            
            // Dodaj stronę do listy, jeśli jeszcze nie istnieje
            if (!_blockedSites.Contains(site))
            {
                _blockedSites.Add(site);
                _config.BlockedSites.Add(site);
                SaveConfig();
                
                // Jeśli blokowanie jest aktywne, zastosuj zmiany
                if (_isBlockingActive)
                {
                    _blocker.BlockSites(_config.BlockedSites);
                }
            }
            
            // Wyczyść pole tekstowe
            SiteTextBox.Text = "";
        }

        private void RemoveSiteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is string site)
            {
                // Usuń stronę z listy
                _blockedSites.Remove(site);
                _config.BlockedSites.Remove(site);
                SaveConfig();
                
                // Jeśli blokowanie jest aktywne, zastosuj zmiany
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
                    "Proszę dodać co najmniej jedną stronę do zablokowania.",
                    "Pusta lista",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            
            _blocker.UseHostsFile = HostsFileCheckBox.IsChecked ?? true;
            _blocker.UseFirewall = FirewallCheckBox.IsChecked ?? true;
            _blocker.UseWfp = WfpCheckBox.IsChecked ?? false;
            
            // Włącz blokowanie
            _config.EnableBlocking();
            SaveConfig();
            
            // Zastosuj blokadę
            _blocker.BlockSites(_config.BlockedSites);
            
            // Zaktualizuj informacje o statusie
            UpdateStatusInfo();
            
            MessageBox.Show(
                $"Blokowanie zostało włączone na maksymalnie {_config.MaxBlockingDuration.TotalMinutes} minut.\n\n" +
                "Wskazówki dla skutecznego blokowania:\n" +
                "1. Zamknij wszystkie okna przeglądarki\n" +
                "2. Wyczyść pamięć podręczną przeglądarki\n" +
                "3. Uruchom przeglądarkę ponownie\n\n" +
                "Niektóre przeglądarki (szczególnie Chrome) mogą przechowywać własną pamięć DNS.",
                "Blokowanie włączone",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        
        private void InitializeScheduleComponents()
        {
            // Ustaw źródło danych dla listy harmonogramu
            ScheduleListBox.ItemsSource = _scheduleItems;
    
            // Wypełnij combo boxy dla dni i godzin
            DayComboBox.ItemsSource = Enum.GetValues(typeof(DayOfWeek));
            DayComboBox.SelectedIndex = 0;
    
            // Wypełnij combo boxy godzin i minut
            for (int i = 0; i < 24; i++)
            {
                StartHourComboBox.Items.Add(i.ToString("00"));
                EndHourComboBox.Items.Add(i.ToString("00"));
            }
            StartHourComboBox.SelectedIndex = 9; // Domyślnie 9:00
            EndHourComboBox.SelectedIndex = 17; // Domyślnie 17:00
    
            for (int i = 0; i < 60; i += 15)
            {
                StartMinuteComboBox.Items.Add(i.ToString("00"));
                EndMinuteComboBox.Items.Add(i.ToString("00"));
            }
            StartMinuteComboBox.SelectedIndex = 0; // 00 minut
            EndMinuteComboBox.SelectedIndex = 0; // 00 minut
    
            // Wypełnij listę elementów harmonogramu z konfiguracji
            foreach (var item in _config.BlockingSchedule)
            {
                _scheduleItems.Add(item);
            }
        }

        private void StopBlockingButton_Click(object sender, RoutedEventArgs e)
        {
            StopBlocking();
        }

        private void EmergencyButton_Click(object sender, RoutedEventArgs e)
        {
            // Włącz tryb awaryjny i usuń blokady
            _config.EnableEmergencyMode();
            _blocker.UnblockSites();
            _blocker.EmergencyRestore(); // Dodaj tę metodę do klasy SiteBlocker
            SaveConfig();
            
            // Zaktualizuj informacje o statusie
            UpdateStatusInfo();
            
            MessageBox.Show(
                "TRYB AWARYJNY został aktywowany. Wszystkie blokady zostały usunięte.",
                "Tryb awaryjny",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void StopBlocking()
        {
            // Wyłącz blokowanie
            _config.DisableBlocking();
            SaveConfig();
            
            // Usuń blokadę
            _blocker.UnblockSites();
            
            // Zaktualizuj informacje o statusie
            UpdateStatusInfo();
            
            MessageBox.Show(
                "Blokowanie zostało wyłączone.",
                "Blokowanie wyłączone",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        
        // Add this method at the end of the class
        public void Dispose()
        {
            _blocker?.Dispose();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            // Zatrzymaj timer
            _timer.Stop();
            
            // Zapytaj, czy wyłączyć blokowanie przy zamykaniu aplikacji
            if (_isBlockingActive)
            {
                MessageBoxResult result = MessageBox.Show(
                    "Czy chcesz wyłączyć blokowanie przed zamknięciem aplikacji?",
                    "Blokowanie aktywne",
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
            // Pobierz wybrane wartości
            DayOfWeek day = (DayOfWeek)DayComboBox.SelectedItem;
            int startHour = int.Parse(StartHourComboBox.SelectedItem.ToString());
            int startMinute = int.Parse(StartMinuteComboBox.SelectedItem.ToString());
            int endHour = int.Parse(EndHourComboBox.SelectedItem.ToString());
            int endMinute = int.Parse(EndMinuteComboBox.SelectedItem.ToString());
    
            // Sprawdź poprawność danych
            if (endHour < startHour || (endHour == startHour && endMinute <= startMinute))
            {
                MessageBox.Show(
                    "Czas zakończenia musi być późniejszy niż czas rozpoczęcia.",
                    "Nieprawidłowy zakres czasu",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
    
            // Utwórz nowy element harmonogramu
            ScheduleItem item = new ScheduleItem
            {
                Day = day,
                StartTime = new TimeSpan(startHour, startMinute, 0),
                EndTime = new TimeSpan(endHour, endMinute, 0),
                IsEnabled = true
            };
    
            // Dodaj do kolekcji
            _scheduleItems.Add(item);
            _config.BlockingSchedule.Add(item);
            SaveConfig();
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
            SaveConfig(); // Zapisz stan po zmianie aktywności elementu harmonogramu
        }

        private void UseScheduleCheckBox_Click(object sender, RoutedEventArgs e)
        {
            // Ustaw flagę używania harmonogramu w konfiguracji
            // Możesz dodać tę flagę do klasy BlockerConfig
            SaveConfig();
        }
    }
}