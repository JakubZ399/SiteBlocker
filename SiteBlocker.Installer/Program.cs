using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace SiteBlocker.Installer
{
    class Program
    {
        static void Main(string[] args)
        {
            // Sprawdź uprawnienia administratora
            if (!IsAdministrator())
            {
                Console.WriteLine("Instalator wymaga uprawnień administratora. Uruchamiam ponownie...");
                RestartAsAdmin();
                return;
            }

            Console.WriteLine("===== INSTALATOR SITEBLOCKER =====");
            Console.WriteLine("1. Zainstaluj usługę i Guardian");
            Console.WriteLine("2. Odinstaluj usługę i Guardian");
            Console.WriteLine("3. Wyjdź");

            string choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    InstallService();
                    InstallGuardian();
                    break;
                case "2":
                    UninstallService();
                    UninstallGuardian();
                    break;
                default:
                    Console.WriteLine("Wyjście z instalatora.");
                    break;
            }

            Console.WriteLine("Naciśnij dowolny klawisz, aby kontynuować...");
            Console.ReadKey();
        }

        static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        static void RestartAsAdmin()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule.FileName,
                UseShellExecute = true,
                Verb = "runas" // Uruchom jako administrator
            };

            try
            {
                Process.Start(startInfo);
            }
            catch (Exception)
            {
                Console.WriteLine("Anulowano podnoszenie uprawnień. Aplikacja wymaga uprawnień administratora.");
            }
        }
        //

        static void InstallService()
        {
            try
            {
                Console.WriteLine("Instalowanie usługi SiteBlocker...");

                // Ścieżka do pliku wykonywalnego usługi
                string servicePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SiteBlocker.Service.exe");

                // Sprawdź, czy plik istnieje
                if (!File.Exists(servicePath))
                {
                    Console.WriteLine($"Błąd: Nie znaleziono pliku {servicePath}");
                    return;
                }

                // Utwórz proces instalacji usługi
                Process process = new Process();
                process.StartInfo.FileName = "sc";
                process.StartInfo.Arguments = $"create SiteBlockerService binPath= \"{servicePath}\" start= auto DisplayName= \"SiteBlocker Service\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"Błąd podczas instalacji usługi. Kod wyjścia: {process.ExitCode}");
                    return;
                }

                // Ustaw usługę do automatycznego restartu przy awarii
                process = new Process();
                process.StartInfo.FileName = "sc";
                process.StartInfo.Arguments = "failure SiteBlockerService reset= 0 actions= restart/60000/restart/60000/restart/60000";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                process.WaitForExit();

                // Uruchom usługę
                process = new Process();
                process.StartInfo.FileName = "sc";
                process.StartInfo.Arguments = "start SiteBlockerService";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                process.WaitForExit();

                Console.WriteLine("Usługa SiteBlocker została pomyślnie zainstalowana i uruchomiona.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas instalacji usługi: {ex.Message}");
            }
        }

        static void UninstallService()
        {
            try
            {
                Console.WriteLine("Odinstalowywanie usługi SiteBlocker...");

                // Zatrzymaj usługę, jeśli działa
                Process processStop = new Process();
                processStop.StartInfo.FileName = "sc";
                processStop.StartInfo.Arguments = "stop SiteBlockerService";
                processStop.StartInfo.UseShellExecute = false;
                processStop.StartInfo.CreateNoWindow = true;
                processStop.Start();
                processStop.WaitForExit();

                // Usuń usługę
                Process processDelete = new Process();
                processDelete.StartInfo.FileName = "sc";
                processDelete.StartInfo.Arguments = "delete SiteBlockerService";
                processDelete.StartInfo.UseShellExecute = false;
                processDelete.StartInfo.CreateNoWindow = true;
                processDelete.Start();
                processDelete.WaitForExit();

                Console.WriteLine("Usługa SiteBlocker została pomyślnie odinstalowana.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas odinstalowywania usługi: {ex.Message}");
            }
        }

        //
        static void InstallGuardian()
        {
            try
            {
                Console.WriteLine("Instalowanie Guardian jako zadanie zaplanowane...");

                string guardianPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SiteBlocker.Guardian.exe");

                if (!File.Exists(guardianPath))
                {
                    Console.WriteLine($"Błąd: Nie znaleziono pliku {guardianPath}");
                    return;
                }

                // Utwórz zadanie zaplanowane, które uruchamia Guardian co 5 minut
                Process process = new Process();
                process.StartInfo.FileName = "schtasks";
                process.StartInfo.Arguments = $"/create /tn \"SiteBlocker Guardian\" /tr \"{guardianPath}\" /sc minute /mo 5 /ru SYSTEM /f";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    Console.WriteLine("Guardian został pomyślnie zainstalowany jako zadanie zaplanowane.");

                    // Uruchom Guardian od razu
                    Process.Start(guardianPath);
                }
                else
                {
                    Console.WriteLine($"Błąd podczas instalacji Guardian. Kod wyjścia: {process.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas instalacji Guardian: {ex.Message}");
            }
        }

        static void UninstallGuardian()
        {
            try
            {
                Console.WriteLine("Odinstalowywanie Guardian...");

                // Usuń zadanie zaplanowane
                Process process = new Process();
                process.StartInfo.FileName = "schtasks";
                process.StartInfo.Arguments = "/delete /tn \"SiteBlocker Guardian\" /f";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                process.WaitForExit();

                // Zatrzymaj wszystkie działające procesy Guardian
                foreach (var proc in Process.GetProcessesByName("SiteBlocker.Guardian"))
                {
                    try
                    {
                        proc.Kill();
                    }
                    catch
                    {
                        // Ignoruj błędy przy zatrzymywaniu procesu
                    }
                }

                Console.WriteLine("Guardian został pomyślnie odinstalowany.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas odinstalowywania Guardian: {ex.Message}");
            }
        }
        //
    }
}