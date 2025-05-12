using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace SiteBlocker.Core;

public class SiteBlocker
{
    private readonly FirewallBlocker _firewallBlocker = new FirewallBlocker();
    private readonly string _hostsPath = @"C:\Windows\System32\drivers\etc\hosts";

    private const string MARKER_START = "# BEGIN SITE_BLOCKER";
    private const string MARKER_END = "# END SITE_BLOCKER";
    
    public SiteBlocker()
    {
        Logger.Log("Inicjalizacja SiteBlocker - czyszczenie starych reguł zapory...");
        try
        {
            _firewallBlocker.UnblockAllDomains();
        }
        catch (Exception ex)
        {
            Logger.Log($"Ostrzeżenie: Nie udało się wyczyścić starych reguł: {ex.Message}");
        }
    }
    
    public bool BlockSites(List<string> sites)
    {
        try
        {
            if (sites == null || sites.Count == 0)
                return false;

            Logger.Log($"Blokowanie {sites.Count} stron...");
        
            bool hostsSuccess = BlockSitesWithHosts(sites);
            bool firewallSuccess = BlockSitesWithFirewall(sites);
        
            FlushDnsCache();
        
            return hostsSuccess || firewallSuccess;
        }
        catch (Exception ex)
        {
            Logger.Log($"Błąd blokowania stron: {ex.Message}");
            return false;
        }
    }

    private bool BlockSitesWithFirewall(List<string> sites)
    {
        bool success = true;
    
        foreach (string site in sites)
        {
            if (!string.IsNullOrWhiteSpace(site))
            {
                if (!_firewallBlocker.BlockDomain(site))
                    success = false;
            }
        }
    
        return success;
    }

private bool BlockSitesWithHosts(List<string> sites)
{
    try
    {
        // Upewnij się, że lista stron nie jest pusta
        if (sites == null || sites.Count == 0)
            return false;

        Logger.Log($"Blokowanie {sites.Count} stron przez plik hosts...");

        // Odczytaj istniejący plik hosts
        string hostsContent = File.ReadAllText(_hostsPath);
        Logger.Log("Odczytano plik hosts");

        // Usuń poprzednią sekcję blokady, jeśli istnieje
        hostsContent = RemoveBlockerSection(hostsContent);
        Logger.Log("Usunięto poprzednią sekcję blokady");

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
                    .TrimEnd('/'); // Usuń końcowy ukośnik

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

                Logger.Log($"Dodano blokadę hosts dla: {baseDomain}");
            }
        }

        blockerSection.AppendLine(MARKER_END);

        // Zapisz zmodyfikowany plik hosts
        File.WriteAllText(_hostsPath, hostsContent + Environment.NewLine + blockerSection.ToString());
        Logger.Log("Zapisano zmodyfikowany plik hosts");

        return true;
    }
    catch (Exception ex)
    {
        Logger.Log($"Błąd podczas blokowania przez plik hosts: {ex.Message}");
        return false;
    }
}

public bool UnblockSites()
{
    try
    {
        Logger.Log("Odblokowywanie stron...");
        
        bool hostsSuccess = UnblockSitesFromHosts();
        
        bool firewallSuccess = _firewallBlocker.UnblockAllDomains();
        Logger.Log($"Usunięto reguły zapory: {firewallSuccess}");
        
        FlushDnsCache();
        
        return hostsSuccess || firewallSuccess;
    }
    catch (Exception ex)
    {
        Logger.Log($"Błąd odblokowywania stron: {ex.Message}");
        return false;
    }
}

    private bool UnblockSitesFromHosts()
    {
        try
        {
            Logger.Log("Odblokowywanie stron z pliku hosts...");
        
            // Odczytaj istniejący plik hosts
            string hostsContent = File.ReadAllText(_hostsPath);

            // Usuń sekcję blokady
            string newContent = RemoveBlockerSection(hostsContent);

            // Zapisz plik tylko jeśli zawartość rzeczywiście się zmieniła
            if (newContent != hostsContent)
            {
                File.WriteAllText(_hostsPath, newContent);
                Logger.Log("Usunięto blokady z pliku hosts");
            }
            else
            {
                Logger.Log("Brak blokad do usunięcia w pliku hosts");
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Log($"Błąd podczas odblokowywania przez plik hosts: {ex.Message}");
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
                 hostsContent[endIndex + MARKER_END.Length] == '\n'
                    ? 1
                    : 0)
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
            Logger.Log($"Ostrzeżenie: Nie udało się odświeżyć pamięci DNS: {ex.Message}");
            // Ignoruj błędy, ale zapisz informację
        }
    }
    
    public bool EmergencyRestore()
    {
        try
        {
            Logger.Log("Przywracanie systemu do normalnego stanu (tryb awaryjny)...");

            // Przywróć plik hosts
            string hostsContent = File.ReadAllText(_hostsPath);
            string newContent = RemoveBlockerSection(hostsContent);
            File.WriteAllText(_hostsPath, newContent);

            // Usuń wszystkie reguły zapory
            _firewallBlocker.UnblockAllDomains();

            // Odśwież DNS
            FlushDnsCache();

            Logger.Log("System został przywrócony do normalnego stanu.");

            return true;
        }
        catch (Exception ex)
        {
            Logger.Log($"Błąd podczas przywracania systemu: {ex.Message}");
            return false;
        }
    }
}