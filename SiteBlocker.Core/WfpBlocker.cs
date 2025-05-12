using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using WinDivertSharp;

namespace SiteBlocker.Core;

public class WfpBlocker : IDisposable
{
    private readonly List<string> _blockedIPs = new List<string>();
    private IntPtr _divertHandle = IntPtr.Zero;
    private Thread? _packetThread = null;
    private bool _isRunning = false;
    private readonly object _lockObj = new object();
    
    // Mapping of domains to IPs for unblocking
    private Dictionary<string, List<string>> _domainToIpsMap = new Dictionary<string, List<string>>();
    
    public bool BlockDomain(string domain)
    {
        try
        {
            Logger.Log($"WFP: Blocking domain: {domain}");
            
            string cleanDomain = CleanDomainName(domain);
            Logger.Log($"WFP: Cleaned domain: {cleanDomain}");
            
            // Resolve IP addresses for domain
            IPAddress[]? addresses;
            try
            {
                addresses = Dns.GetHostAddresses(cleanDomain);
                Logger.Log($"WFP: Found {addresses.Length} IP addresses for domain {cleanDomain}");
            }
            catch (Exception ex)
            {
                Logger.Log($"WFP: DNS Error - Cannot find IP for domain {cleanDomain}: {ex.Message}");
                return false;
            }
            
            if (addresses.Length == 0)
            {
                Logger.Log($"WFP: No IP addresses found for domain {cleanDomain}");
                return false;
            }
            
            // Store domain to IPs mapping for later unblocking
            if (!_domainToIpsMap.ContainsKey(cleanDomain))
            {
                _domainToIpsMap[cleanDomain] = new List<string>();
            }
            
            lock (_lockObj)
            {
                foreach (IPAddress address in addresses)
                {
                    string ipAddress = address.ToString();
                    Logger.Log($"WFP: Adding IP {ipAddress} to blocked list");
                    
                    if (!_blockedIPs.Contains(ipAddress))
                    {
                        _blockedIPs.Add(ipAddress);
                    }
                    
                    if (!_domainToIpsMap[cleanDomain].Contains(ipAddress))
                    {
                        _domainToIpsMap[cleanDomain].Add(ipAddress);
                    }
                }
            }
            
            // Start WFP interception if not already running
            if (!_isRunning)
            {
                StartInterception();
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Logger.Log($"WFP: Critical error blocking domain: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }
    
    public bool UnblockDomain(string domain)
    {
        try
        {
            Logger.Log($"WFP: Unblocking domain: {domain}");
            
            string cleanDomain = CleanDomainName(domain);
            
            if (_domainToIpsMap.TryGetValue(cleanDomain, out List<string>? ips) && ips != null)
            {
                lock (_lockObj)
                {
                    foreach (string ip in ips)
                    {
                        _blockedIPs.Remove(ip);
                        Logger.Log($"WFP: Removed IP {ip} from blocked list");
                    }
                    
                    _domainToIpsMap.Remove(cleanDomain);
                }
                
                // If no more blocked IPs, stop interception
                if (_blockedIPs.Count == 0 && _isRunning)
                {
                    StopInterception();
                }
                
                return true;
            }
            
            Logger.Log($"WFP: Domain {cleanDomain} was not found in blocked list");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Log($"WFP: Error unblocking domain: {ex.Message}");
            return false;
        }
    }
    
    public bool UnblockAllDomains()
    {
        try
        {
            Logger.Log("WFP: Unblocking all domains");
            
            lock (_lockObj)
            {
                _blockedIPs.Clear();
                _domainToIpsMap.Clear();
            }
            
            if (_isRunning)
            {
                StopInterception();
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Logger.Log($"WFP: Error unblocking all domains: {ex.Message}");
            return false;
        }
    }
    
    private void StartInterception()
    {
        try
        {
            Logger.Log("WFP: Starting packet interception");
            
            _isRunning = true;
            
            // Create a new thread for packet processing
            _packetThread = new Thread(ProcessPackets);
            _packetThread.IsBackground = true;
            _packetThread.Start();
        }
        catch (Exception ex)
        {
            Logger.Log($"WFP: Error starting interception: {ex.Message}");
            _isRunning = false;
        }
    }
    
    private void StopInterception()
    {
        try
        {
            Logger.Log("WFP: Stopping packet interception");
            
            _isRunning = false;
            
            // Close the divert handle to stop packet capturing
            if (_divertHandle != IntPtr.Zero)
            {
                WinDivert.WinDivertClose(_divertHandle);
                _divertHandle = IntPtr.Zero;
            }
            
            // Wait for the thread to exit
            _packetThread?.Join(1000);
        }
        catch (Exception ex)
        {
            Logger.Log($"WFP: Error stopping interception: {ex.Message}");
        }
    }
    
private void ProcessPackets()
{
    try
    {
        // Filtruj pakiety TCP dla ruchu HTTP/HTTPS
        string filter = "tcp and (tcp.DstPort == 80 or tcp.DstPort == 443)";
        _divertHandle = WinDivert.WinDivertOpen(filter, WinDivertLayer.Network, 0, WinDivertOpenFlags.None);
        
        if (_divertHandle == IntPtr.Zero)
        {
            throw new Exception("Failed to open WinDivert handle");
        }
        
        Logger.Log("WFP: Packet interception started successfully");
        
        // Utwórz obiekt WinDivertBuffer
        WinDivertBuffer packetBuffer = new WinDivertBuffer();
        
        while (_isRunning)
        {
            try
            {
                uint readLength = 0;
                WinDivertAddress addr = new WinDivertAddress();
                
                // Odbierz pakiet
                if (!WinDivert.WinDivertRecv(_divertHandle, packetBuffer, ref addr, ref readLength))
                {
                    Thread.Sleep(10);
                    continue;
                }
                
                // Analizuj pakiet tylko jeśli jest dostatecznie duży
                if (readLength < 40) // Minimum dla nagłówka IPv4 + TCP
                {
                    WinDivert.WinDivertSend(_divertHandle, packetBuffer, readLength, ref addr);
                    continue;
                }
                
                // Kopiuj dane z bufora WinDivert do tablicy bajtów dla analizy
                byte[] packetData = new byte[readLength];
                
                for (int i = 0; i < readLength; i++)
                {
                    packetData[i] = packetBuffer[i];
                }
                
                // Proste wyodrębnianie adresów IP z pakietu
                string? srcIp = null;
                string? dstIp = null;
                
                // Sprawdź wersję protokołu IP
                byte ipVersion = (byte)((packetData[0] & 0xF0) >> 4);
                
                if (ipVersion == 4) // IPv4
                {
                    // Adresy IPv4 znajdują się w określonych miejscach w nagłówku
                    srcIp = $"{packetData[12]}.{packetData[13]}.{packetData[14]}.{packetData[15]}";
                    dstIp = $"{packetData[16]}.{packetData[17]}.{packetData[18]}.{packetData[19]}";
                    
                    Logger.Log($"WFP: Detected IPv4 packet from {srcIp} to {dstIp}");
                }
                
                // Sprawdź czy któryś z adresów IP jest na liście blokowanych
                bool shouldBlock = false;
                
                lock (_lockObj)
                {
                    if ((srcIp != null && _blockedIPs.Contains(srcIp)) || 
                        (dstIp != null && _blockedIPs.Contains(dstIp)))
                    {
                        shouldBlock = true;
                        Logger.Log($"WFP: Blocking packet between {srcIp} and {dstIp}");
                    }
                }
                
                // Jeśli pakiet nie powinien być zablokowany, przekaż go dalej
                if (!shouldBlock)
                {
                    WinDivert.WinDivertSend(_divertHandle, packetBuffer, readLength, ref addr);
                }
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    Logger.Log($"WFP: Error processing packet: {ex.Message}");
                    Thread.Sleep(100);
                }
            }
        }
    }
    catch (Exception ex)
    {
        Logger.Log($"WFP: Critical error in packet processing thread: {ex.Message}\n{ex.StackTrace}");
    }
    finally
    {
        if (_divertHandle != IntPtr.Zero)
        {
            WinDivert.WinDivertClose(_divertHandle);
            _divertHandle = IntPtr.Zero;
        }
        Logger.Log("WFP: Packet interception stopped");
    }
}
    
    private string CleanDomainName(string domain)
    {
        string cleanDomain = domain
            .Replace("http://", "")
            .Replace("https://", "")
            .TrimEnd('/');
            
        int pathIndex = cleanDomain.IndexOf('/');
        if (pathIndex > 0)
            cleanDomain = cleanDomain.Substring(0, pathIndex);
        
        int queryIndex = cleanDomain.IndexOf('?');
        if (queryIndex > 0)
            cleanDomain = cleanDomain.Substring(0, queryIndex);
            
        if (cleanDomain.StartsWith("www."))
            cleanDomain = cleanDomain.Substring(4);
            
        return cleanDomain;
    }
    
    public void Dispose()
    {
        StopInterception();
    }
}