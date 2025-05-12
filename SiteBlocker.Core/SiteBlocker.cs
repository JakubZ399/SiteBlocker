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

        Console.WriteLine($"Blokowanie {sites.Count} stron...");
        
        // Odczytaj istniejący plik hosts
        string hostsContent = File.ReadAllText(_hostsPath);
        Console.WriteLine("Odczytano plik hosts");

        // Usuń poprzednią sekcję blokady, jeśli istnieje
        hostsContent = RemoveBlockerSection(hostsContent);
        Console.WriteLine("Usunięto poprzednią sekcję blokady");

        // Dodaj nową sekcję blokowania
        StringBuilder blockerSection = new StringBuilder();
        blockerSection.AppendLine(MARKER_START);
        
        foreach (string site in sites)
        {
            if (!string.IsNullOrWhiteSpace(site))
            {
                // Dokładniejsze czyszczenie URL
                string cleanSite = site
                    .Replace("http://", "")
                    .Replace("https://", "")
                    .TrimEnd('/');  // Usuń końcowy ukośnik
                
                // Wyodrębnij samą domenę (bez ścieżki i parametrów)
                int pathIndex = cleanSite.IndexOf('/');
                if (pathIndex > 0)
                {
                    cleanSite = cleanSite.Substring(0, pathIndex);
                }
                
                int queryIndex = cleanSite.IndexOf('?');
                if (queryIndex > 0)
                {
                    cleanSite = cleanSite.Substring(0, queryIndex);
                }
                
                // Dodaj wpis dla głównej domeny
                string baseDomain = cleanSite;
                if (baseDomain.StartsWith("www."))
                {
                    baseDomain = baseDomain.Substring(4);
                }
                
                // Dodaj wszystkie warianty domeny
                blockerSection.AppendLine($"127.0.0.1 {baseDomain}");
                blockerSection.AppendLine($"127.0.0.1 www.{baseDomain}");
                
                Console.WriteLine($"Dodano blokadę dla: {baseDomain}");
            }
        }
        
        blockerSection.AppendLine(MARKER_END);
        
        // Zapisz zmodyfikowany plik hosts
        File.WriteAllText(_hostsPath, hostsContent + Environment.NewLine + blockerSection.ToString());
        Console.WriteLine("Zapisano zmodyfikowany plik hosts");
        
        // Bardziej dokładne odświeżenie DNS cache
        FlushDnsCache();
        Console.WriteLine("Odświeżono pamięć DNS");
        
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"BŁĄD podczas blokowania stron: {ex.Message}");
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
            // 1. Standardowe odświeżenie DNS cache
            ProcessStartInfo ipConfigPsi = new ProcessStartInfo("ipconfig", "/flushdns")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                Verb = "runas" // Uruchom jako administrator
            };
        
            using (Process ipConfigProcess = Process.Start(ipConfigPsi))
            {
                ipConfigProcess?.WaitForExit();
            }
        
            // 2. Restart usługi DNS Client (opcjonalnie, może wymagać dodatkowych uprawnień)
            ProcessStartInfo dnsRestartPsi = new ProcessStartInfo("net", "stop dnscache")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                Verb = "runas"
            };
        
            using (Process dnsStopProcess = Process.Start(dnsRestartPsi))
            {
                dnsStopProcess?.WaitForExit();
            }
        
            ProcessStartInfo dnsStartPsi = new ProcessStartInfo("net", "start dnscache")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                Verb = "runas"
            };
        
            using (Process dnsStartProcess = Process.Start(dnsStartPsi))
            {
                dnsStartProcess?.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ostrzeżenie: Nie udało się odświeżyć pamięci DNS: {ex.Message}");
            // Ignoruj błędy, ale zapisz informację
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