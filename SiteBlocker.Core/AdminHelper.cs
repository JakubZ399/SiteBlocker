using System;
using System.Diagnostics;
using System.Security.Principal;

namespace SiteBlocker.Core;

public static class AdminHelper
{
    // Sprawdza, czy aplikacja działa z uprawnieniami administratora
    public static bool IsRunningAsAdmin()
    {
        WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
    
    // Uruchamia aplikację ponownie z uprawnieniami administratora
    public static void RestartAsAdmin()
    {
        if (IsRunningAsAdmin())
            return; // Już działa jako admin
        
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule.FileName,
                UseShellExecute = true,
                Verb = "runas" // Uruchom jako administrator
            };
            
            Process.Start(startInfo);
            
            // Zamknij bieżący proces
            Environment.Exit(0);
        }
        catch (Exception)
        {
            // Użytkownik anulował podniesienie uprawnień lub wystąpił inny błąd
        }
    }
}