using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace SiteBlocker.Core;

public class SiteBlocker
{
    // Ścieżka do pliku hosts w systemie Windows
    private readonly string _hostsPath = @"C:\Windows\System32\drivers\etc\hosts";
    
    // Znaczniki oznaczające naszą sekcję w pliku hosts
    private const string MARKER_START = "# BEGIN SITE_BLOCKER";
    private const string MARKER_END = "# END SITE_BLOCKER";
    
    // Metoda blokująca listę stron
    public bool BlockSites(List<string> sites)
    {
        try
        {
            // Upewnij się, że lista stron nie jest pusta
            if (sites == null || sites.Count == 0)
                return false;

            // Odczytaj istniejący plik hosts
            string hostsContent = File.ReadAllText(_hostsPath);

            // Usuń poprzednią sekcję blokady, jeśli istnieje
            hostsContent = RemoveBlockerSection(hostsContent);

            // Dodaj nową sekcję blokowania
            StringBuilder blockerSection = new StringBuilder();
            blockerSection.AppendLine(MARKER_START);
            
            foreach (string site in sites)
            {
                if (!string.IsNullOrWhiteSpace(site))
                {
                    // Upewnij się, że domena nie zawiera http:// lub https://
                    string cleanSite = site
                        .Replace("http://", "")
                        .Replace("https://", "")
                        .Replace("www.", "");
                    
                    // Dodaj wpisy dla oryginalnej domeny i jej wersji z www
                    blockerSection.AppendLine($"127.0.0.1 {cleanSite}");
                    blockerSection.AppendLine($"127.0.0.1 www.{cleanSite}");
                }
            }
            
            blockerSection.AppendLine(MARKER_END);
            
            // Zapisz zmodyfikowany plik hosts (wymaga uprawnień administratora)
            File.WriteAllText(_hostsPath, hostsContent + Environment.NewLine + blockerSection.ToString());
            
            // Odświeżenie DNS cache, aby zmiany zostały natychmiast zastosowane
            FlushDnsCache();
            
            return true;
        }
        catch (Exception ex)
        {
            // W produkcyjnej wersji zapisz błąd do logów
            Console.WriteLine($"Błąd podczas blokowania stron: {ex.Message}");
            return false;
        }
    }
    
    // Metoda usuwająca blokadę
    public bool UnblockSites()
    {
        try
        {
            // Odczytaj istniejący plik hosts
            string hostsContent = File.ReadAllText(_hostsPath);

            // Usuń sekcję blokady
            string newContent = RemoveBlockerSection(hostsContent);
            
            // Zapisz plik tylko jeśli zawartość rzeczywiście się zmieniła
            if (newContent != hostsContent)
            {
                File.WriteAllText(_hostsPath, newContent);
                FlushDnsCache();
            }
            
            return true;
        }
        catch (Exception ex)
        {
            // W produkcyjnej wersji zapisz błąd do logów
            Console.WriteLine($"Błąd podczas odblokowywania stron: {ex.Message}");
            return false;
        }
    }
    
    // Pomocnicza metoda do usuwania sekcji blokady z pliku hosts
    private string RemoveBlockerSection(string hostsContent)
    {
        int startIndex = hostsContent.IndexOf(MARKER_START);
        int endIndex = hostsContent.IndexOf(MARKER_END);

        if (startIndex >= 0 && endIndex >= 0 && endIndex > startIndex)
        {
            // Usuń całą sekcję blokady wraz z końcowym znacznikiem i nową linią
            return hostsContent.Remove(
                startIndex, 
                endIndex + MARKER_END.Length - startIndex + 
                (endIndex + MARKER_END.Length < hostsContent.Length && 
                 hostsContent[endIndex + MARKER_END.Length] == '\n' ? 1 : 0)
            );
        }
        
        return hostsContent;
    }
    
    // Metoda odświeżająca DNS cache
    private void FlushDnsCache()
    {
        try
        {
            // Uruchom komendę ipconfig /flushdns (wymaga uprawnień administratora)
            ProcessStartInfo psi = new ProcessStartInfo("ipconfig", "/flushdns")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                Verb = "runas" // Uruchom jako administrator
            };
            
            using Process process = Process.Start(psi);
            process?.WaitForExit();
        }
        catch (Exception)
        {
            // Ignoruj błędy przy odświeżaniu DNS cache
        }
    }
    
    public bool EmergencyRestore()
    {
        try
        {
            // Przywróć plik hosts do bezpiecznego stanu
            string hostsContent = File.ReadAllText(_hostsPath);
            string newContent = RemoveBlockerSection(hostsContent);
            File.WriteAllText(_hostsPath, newContent);
            FlushDnsCache();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd podczas przywracania systemu: {ex.Message}");
            return false;
        }
    }
}