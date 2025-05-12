using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SiteBlocker.Core;

public class BlockerConfig
{
    // Lista zablokowanych stron
    public List<string> BlockedSites { get; set; } = new List<string>();
    
    // Harmonogram blokowania
    public List<ScheduleItem> BlockingSchedule { get; set; } = new List<ScheduleItem>();
    
    // Hasło do odblokowania (przechowywane w formie zaszyfrowanej)
    public string PasswordHash { get; set; }
    
    // Czy blokowanie jest aktywne
    public bool IsActive { get; set; }
    
    // Maksymalny czas blokowania (w godzinach)
    public int MaxBlockingHours { get; set; } = 8;
    
    public DateTime? BlockingStartTime { get; set; }
    public TimeSpan MaxBlockingDuration { get; set; } = TimeSpan.FromMinutes(5); // 5 minut na testy
    public bool IsEmergencyModeEnabled { get; set; }

    // Ścieżka do domyślnego pliku konfiguracyjnego
    public static string DefaultConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SiteBlocker", 
        "config.json");

    // Zapisz konfigurację do pliku
    public void SaveToFile(string path)
    {
        // Upewnij się, że katalog istnieje
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Serializuj konfigurację do JSON
        string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        
        // W wersji produkcyjnej dodamy szyfrowanie:
        // byte[] encryptedData = EncryptionHelper.Encrypt(json);
        // File.WriteAllBytes(path, encryptedData);
        
        // Na razie zapisujemy jako zwykły tekst dla uproszczenia
        File.WriteAllText(path, json);
    }

    // Załaduj konfigurację z pliku
    public static BlockerConfig LoadFromFile(string path)
    {
        // Jeśli plik nie istnieje, zwróć nową konfigurację
        if (!File.Exists(path))
            return new BlockerConfig();

        try
        {
            // Odczytaj zawartość pliku
            string json = File.ReadAllText(path);
            
            // Deserializuj z powrotem do obiektu
            return JsonSerializer.Deserialize<BlockerConfig>(json) ?? new BlockerConfig();
            
            // W wersji produkcyjnej dodamy deszyfrowanie:
            // byte[] encryptedData = File.ReadAllBytes(path);
            // string json = EncryptionHelper.Decrypt(encryptedData);
            // return JsonSerializer.Deserialize<BlockerConfig>(json);
        }
        catch (Exception)
        {
            // W przypadku błędu, zwróć nową konfigurację
            return new BlockerConfig();
        }
    }
    
    public bool ShouldBeActiveNow()
    {
        // Jeśli tryb awaryjny jest włączony, nie blokuj
        if (IsEmergencyModeEnabled)
            return false;
        
        // Jeśli blokowanie nie jest włączone, nie blokuj
        if (!IsActive)
            return false;
        
        // Sprawdź, czy nie upłynął maksymalny czas blokady
        if (BlockingStartTime.HasValue)
        {
            TimeSpan elapsed = DateTime.Now - BlockingStartTime.Value;
            if (elapsed > MaxBlockingDuration)
                return false;
        }
    
        // Sprawdź, czy obecny czas jest w harmonogramie
        return IsTimeInSchedule();
    }

// Sprawdza, czy obecny czas pasuje do harmonogramu
    private bool IsTimeInSchedule()
    {
        // Jeśli harmonogram jest pusty, zawsze zwracaj true
        if (BlockingSchedule == null || BlockingSchedule.Count == 0)
            return true;
        
        // Sprawdź każdy element harmonogramu
        foreach (var item in BlockingSchedule)
        {
            if (item.IsActiveNow())
                return true;
        }
    
        return false;
    }

// Włącza blokowanie i ustawia czas rozpoczęcia
    public void EnableBlocking()
    {
        IsActive = true;
        BlockingStartTime = DateTime.Now;
        IsEmergencyModeEnabled = false;
    }

// Wyłącza blokowanie
    public void DisableBlocking()
    {
        IsActive = false;
    }

// Włącza tryb awaryjny
    public void EnableEmergencyMode()
    {
        IsEmergencyModeEnabled = true;
    }
}