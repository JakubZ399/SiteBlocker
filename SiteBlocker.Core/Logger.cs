using System;
using System.IO;

namespace SiteBlocker.Core;

public static class Logger
{
    private static readonly string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SiteBlocker", 
        "siteblocker.log");
        
    static Logger()
    {
        try
        {
            // Ensure directory exists
            string? directory = Path.GetDirectoryName(_logPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // Clear log file at startup
            File.WriteAllText(_logPath, $"=== SiteBlocker Log - {DateTime.Now} ===\r\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize logger: {ex.Message}");
        }
    }
    
    public static void Log(string message)
    {
        try
        {
            string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}\r\n";
            
            // Write to file
            File.AppendAllText(_logPath, logEntry);
            
            // Also output to console
            Console.WriteLine(message);
        }
        catch
        {
            // Ignore logging errors
        }
    }
}