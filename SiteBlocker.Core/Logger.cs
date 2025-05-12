namespace SiteBlocker.Core;

public static class Logger
{
    private static string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SiteBlocker", 
        "siteblocker.log");
        
    static Logger()
    {
        // Upewnij się, że katalog istnieje
        string directory = Path.GetDirectoryName(_logPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // Wyczyść plik logów przy starcie
        File.WriteAllText(_logPath, $"=== SiteBlocker Log - {DateTime.Now} ===\r\n");
    }
    
    public static void Log(string message)
    {
        try
        {
            string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}\r\n";
            
            // Zapisz do pliku
            File.AppendAllText(_logPath, logEntry);
            
            // Wyświetl również w konsoli
            Console.WriteLine(message);
        }
        catch
        {
            // Ignoruj błędy logowania
        }
    }
}