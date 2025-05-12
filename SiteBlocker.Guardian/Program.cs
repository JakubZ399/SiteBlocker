using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using SiteBlocker.Core;

namespace SiteBlocker.Guardian
{
    class Program
    {
        private const string ServiceName = "SiteBlockerService";
        private static readonly int CheckIntervalMs = 5000; // 5 sekund
        
        static void Main(string[] args)
        {
            // Sprawdź uprawnienia administratora
            if (!AdminHelper.IsRunningAsAdmin())
            {
                Console.WriteLine("Guardian wymaga uprawnień administratora!");
                AdminHelper.RestartAsAdmin();
                return;
            }
            
            Console.WriteLine("SiteBlocker Guardian uruchomiony - monitorowanie usługi głównej...");
            
            // Pętla monitorowania
            while (true)
            {
                try
                {
                    // Sprawdź, czy usługa działa
                    CheckAndRestartService();
                    
                    // Sprawdź, czy blokady są aktywne
                    CheckBlockingStatus();
                    
                    Thread.Sleep(CheckIntervalMs);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Błąd: {ex.Message}");
                    Thread.Sleep(10000); // Dłuższe opóźnienie przy błędzie
                }
            }
        }
        
        private static void CheckAndRestartService()
        {
            try
            {
                ServiceController sc = new ServiceController(ServiceName);
                
                if (sc.Status != ServiceControllerStatus.Running)
                {
                    Console.WriteLine("Usługa nie działa! Próba ponownego uruchomienia...");
                    
                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                        Console.WriteLine("Usługa została uruchomiona ponownie.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Nie można uruchomić usługi: {ex.Message}");
                
                // Jeśli usługa nie istnieje, możemy spróbować ją zainstalować
                if (ex.Message.Contains("nie istnieje") || ex.Message.Contains("does not exist"))
                {
                    TryInstallService();
                }
            }
        }
        
        private static void CheckBlockingStatus()
        {
            try
            {
                // Wczytaj konfigurację
                BlockerConfig config = BlockerConfig.LoadFromFile(BlockerConfig.DefaultConfigPath);
                
                // Sprawdź, czy blokada powinna być aktywna
                if (config.IsActive && config.ShouldBeActiveNow() && config.BlockedSites.Count > 0)
                {
                    Console.WriteLine("Sprawdzam status blokad...");
                    
                    // Wymuszaj blokadę jako zabezpieczenie
                    using (SiteBlocker.Core.SiteBlocker blocker = new SiteBlocker.Core.SiteBlocker())
                    {
                        blocker.UseHostsFile = true;
                        blocker.UseFirewall = true;
                        blocker.UseWfp = true;
                        blocker.BlockSites(config.BlockedSites);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas sprawdzania statusu blokad: {ex.Message}");
            }
        }
        
        private static void TryInstallService()
        {
            try
            {
                Console.WriteLine("Próba zainstalowania usługi...");
                
                // Znajdź ścieżkę do usługi
                string servicePath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, 
                    "SiteBlocker.Service.exe");
                
                if (!System.IO.File.Exists(servicePath))
                {
                    Console.WriteLine($"Nie znaleziono pliku usługi: {servicePath}");
                    return;
                }
                
                // Utwórz proces do instalacji usługi
                Process process = new Process();
                process.StartInfo.FileName = "sc";
                process.StartInfo.Arguments = $"create {ServiceName} binPath= \"{servicePath}\" start= auto DisplayName= \"SiteBlocker Service\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                process.WaitForExit();
                
                if (process.ExitCode == 0)
                {
                    Console.WriteLine("Usługa została zainstalowana pomyślnie.");
                    
                    // Uruchom usługę
                    process = new Process();
                    process.StartInfo.FileName = "sc";
                    process.StartInfo.Arguments = $"start {ServiceName}";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    process.WaitForExit();
                    
                    Console.WriteLine("Usługa została uruchomiona.");
                }
                else
                {
                    Console.WriteLine("Nie udało się zainstalować usługi.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas instalacji usługi: {ex.Message}");
            }
        }
    }
}