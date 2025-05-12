using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace SiteBlocker.Core;

public class SiteBlocker : IDisposable
{
    private readonly FirewallBlocker _firewallBlocker = new FirewallBlocker();
    private readonly WfpBlocker _wfpBlocker = new WfpBlocker();
    private readonly string _hostsPath = @"C:\Windows\System32\drivers\etc\hosts";

    private const string MARKER_START = "# BEGIN SITE_BLOCKER";
    private const string MARKER_END = "# END SITE_BLOCKER";
    
    // Blocking methods flags
    public bool UseHostsFile { get; set; } = true;
    public bool UseFirewall { get; set; } = true;
    public bool UseWfp { get; set; } = false; // Default to false since it's more advanced
    
    public SiteBlocker()
    {
        Logger.Log("Initializing SiteBlocker - clearing old firewall rules...");
        try
        {
            _firewallBlocker.UnblockAllDomains();
            _wfpBlocker.UnblockAllDomains();
        }
        catch (Exception ex)
        {
            Logger.Log($"Warning: Failed to clear old rules: {ex.Message}");
        }
    }
    
    public bool BlockSites(List<string> sites)
    {
        try
        {
            if (sites == null || sites.Count == 0)
                return false;

            Logger.Log($"Blocking {sites.Count} sites...");
            
            bool success = true;
            
            if (UseHostsFile)
            {
                bool hostsSuccess = BlockSitesWithHosts(sites);
                if (!hostsSuccess) success = false;
            }
            
            if (UseFirewall)
            {
                bool firewallSuccess = BlockSitesWithFirewall(sites);
                if (!firewallSuccess) success = false;
            }
            
            if (UseWfp)
            {
                bool wfpSuccess = BlockSitesWithWfp(sites);
                if (!wfpSuccess) success = false;
            }
            
            FlushDnsCache();
            
            return success;
        }
        catch (Exception ex)
        {
            Logger.Log($"Error blocking sites: {ex.Message}");
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
    
    private bool BlockSitesWithWfp(List<string> sites)
    {
        bool success = true;
        
        foreach (string site in sites)
        {
            if (!string.IsNullOrWhiteSpace(site))
            {
                if (!_wfpBlocker.BlockDomain(site))
                    success = false;
            }
        }
        
        return success;
    }
    
    private bool BlockSitesWithHosts(List<string> sites)
    {
        try
        {
            // Make sure the list of sites is not empty
            if (sites == null || sites.Count == 0)
                return false;

            Logger.Log($"Blocking {sites.Count} sites via hosts file...");

            // Read existing hosts file
            string hostsContent = File.ReadAllText(_hostsPath);
            Logger.Log("Read hosts file");

            // Remove previous blocker section if exists
            hostsContent = RemoveBlockerSection(hostsContent);
            Logger.Log("Removed previous blocker section");

            // Add new blocker section
            StringBuilder blockerSection = new StringBuilder();
            blockerSection.AppendLine(MARKER_START);

            foreach (string site in sites)
            {
                if (!string.IsNullOrWhiteSpace(site))
                {
                    // Clean up URL
                    string cleanSite = site
                        .Replace("http://", "")
                        .Replace("https://", "")
                        .TrimEnd('/');

                    // Extract domain only (no path or parameters)
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

                    // Get base domain
                    string baseDomain = cleanSite;
                    if (baseDomain.StartsWith("www."))
                    {
                        baseDomain = baseDomain.Substring(4);
                    }

                    // Add all domain variants
                    blockerSection.AppendLine($"127.0.0.1 {baseDomain}");
                    blockerSection.AppendLine($"127.0.0.1 www.{baseDomain}");

                    Logger.Log($"Added hosts block for: {baseDomain}");
                }
            }

            blockerSection.AppendLine(MARKER_END);

            // Save modified hosts file
            File.WriteAllText(_hostsPath, hostsContent + Environment.NewLine + blockerSection.ToString());
            Logger.Log("Saved modified hosts file");

            return true;
        }
        catch (Exception ex)
        {
            Logger.Log($"Error blocking via hosts file: {ex.Message}");
            return false;
        }
    }
    
    public bool UnblockSites()
    {
        try
        {
            Logger.Log("Unblocking sites...");
            
            bool success = true;
            
            if (UseHostsFile)
            {
                bool hostsSuccess = UnblockSitesFromHosts();
                if (!hostsSuccess) success = false;
            }
            
            if (UseFirewall)
            {
                bool firewallSuccess = _firewallBlocker.UnblockAllDomains();
                Logger.Log($"Removed firewall rules: {firewallSuccess}");
                if (!firewallSuccess) success = false;
            }
            
            if (UseWfp)
            {
                bool wfpSuccess = _wfpBlocker.UnblockAllDomains();
                Logger.Log($"Removed WFP filters: {wfpSuccess}");
                if (!wfpSuccess) success = false;
            }
            
            FlushDnsCache();
            
            return success;
        }
        catch (Exception ex)
        {
            Logger.Log($"Error unblocking sites: {ex.Message}");
            return false;
        }
    }
    
    private bool UnblockSitesFromHosts()
    {
        try
        {
            Logger.Log("Unblocking sites from hosts file...");
        
            // Read existing hosts file
            string hostsContent = File.ReadAllText(_hostsPath);

            // Remove blocker section
            string newContent = RemoveBlockerSection(hostsContent);

            // Save file only if content actually changed
            if (newContent != hostsContent)
            {
                File.WriteAllText(_hostsPath, newContent);
                Logger.Log("Removed blocks from hosts file");
            }
            else
            {
                Logger.Log("No blocks to remove from hosts file");
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Log($"Error unblocking from hosts file: {ex.Message}");
            return false;
        }
    }
    
    // Helper method to remove blocker section from hosts file
    private string RemoveBlockerSection(string hostsContent)
    {
        int startIndex = hostsContent.IndexOf(MARKER_START);
        int endIndex = hostsContent.IndexOf(MARKER_END);

        if (startIndex >= 0 && endIndex >= 0 && endIndex > startIndex)
        {
            // Remove entire blocker section including end marker and newline
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
    
    private void FlushDnsCache()
    {
        try
        {
            // Standard DNS cache refresh
            ProcessStartInfo ipConfigPsi = new ProcessStartInfo("ipconfig", "/flushdns")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                Verb = "runas"
            };

            using (Process ipConfigProcess = Process.Start(ipConfigPsi))
            {
                ipConfigProcess?.WaitForExit();
            }

            // Restart DNS Client service (optional, may require additional permissions)
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
            Logger.Log($"Warning: Failed to refresh DNS cache: {ex.Message}");
        }
    }
    
    public bool EmergencyRestore()
    {
        try
        {
            Logger.Log("Restoring system to normal state (emergency mode)...");

            // Restore hosts file
            string hostsContent = File.ReadAllText(_hostsPath);
            string newContent = RemoveBlockerSection(hostsContent);
            File.WriteAllText(_hostsPath, newContent);

            // Remove all firewall rules
            _firewallBlocker.UnblockAllDomains();
            
            // Remove all WFP filters
            _wfpBlocker.UnblockAllDomains();

            // Refresh DNS
            FlushDnsCache();

            Logger.Log("System restored to normal state.");

            return true;
        }
        catch (Exception ex)
        {
            Logger.Log($"Error during system restoration: {ex.Message}");
            return false;
        }
    }
    
    public void Dispose()
    {
        _wfpBlocker.Dispose();
    }
}