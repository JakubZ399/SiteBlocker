using System;
using System.Collections.Generic;
using System.Net;
using NetFwTypeLib;

namespace SiteBlocker.Core;

public class FirewallBlocker
{
    private const string RULE_PREFIX = "SiteBlocker-";
    
    public bool BlockDomain(string domain)
    {
        try
        {
            Logger.Log($"Blokuję domenę: {domain}");
            
            string cleanDomain = CleanDomainName(domain);
            Logger.Log($"Oczyszczona domena: {cleanDomain}");
            
            // Rozwiązanie nazwy domeny na adres IP
            IPAddress[] addresses;
            try 
            {
                addresses = Dns.GetHostAddresses(cleanDomain);
                Logger.Log($"Znaleziono {addresses.Length} adresów IP dla domeny {cleanDomain}");
            }
            catch (Exception ex)
            {
                Logger.Log($"Błąd DNS: Nie można znaleźć adresu IP dla domeny {cleanDomain}: {ex.Message}");
                return false;
            }
            
            if (addresses.Length == 0)
            {
                Logger.Log($"Nie znaleziono adresów IP dla domeny {cleanDomain}");
                return false;
            }
            
            // Przed utworzeniem nowych reguł, usuńmy wszystkie istniejące dla tej domeny
            // To zapobiega problemom z duplikacją reguł
            UnblockDomain(domain);
            
            bool success = true;
            foreach (IPAddress address in addresses)
            {
                string ipAddress = address.ToString();
                Logger.Log($"Tworzę regułę blokującą dla IP: {ipAddress}");
                
                if (!AddFirewallRule(cleanDomain, ipAddress))
                {
                    Logger.Log($"Nie udało się zablokować IP: {ipAddress}");
                    success = false;
                }
            }
            
            return success;
        }
        catch (Exception ex)
        {
            Logger.Log($"Krytyczny błąd blokowania domeny: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }
    
    public bool UnblockDomain(string domain)
    {
        try
        {
            Logger.Log($"Odblokowuję domenę: {domain}");
            
            string cleanDomain = CleanDomainName(domain);
            string rulePrefix = $"{RULE_PREFIX}{cleanDomain}";
            
            return RemoveFirewallRules(rulePrefix);
        }
        catch (Exception ex)
        {
            Logger.Log($"Błąd odblokowywania domeny: {ex.Message}");
            return false;
        }
    }
    
    public bool UnblockAllDomains()
    {
        try
        {
            Logger.Log("Usuwam wszystkie reguły SiteBlocker z zapory...");
            
            // Utwórz instancję polityki zapory
            Type netFwPolicy2Type = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(netFwPolicy2Type);
            
            // Znajdź i usuń wszystkie reguły z naszym prefiksem
            List<string> rulesToRemove = new List<string>();
            
            // Najpierw zbieramy nazwy wszystkich reguł do usunięcia
            foreach (INetFwRule rule in firewallPolicy.Rules)
            {
                if (rule.Name != null && rule.Name.StartsWith(RULE_PREFIX))
                {
                    Logger.Log($"Znaleziono regułę do usunięcia: {rule.Name}");
                    rulesToRemove.Add(rule.Name);
                }
            }
            
            // Teraz usuwamy wszystkie znalezione reguły
            int removedCount = 0;
            foreach (string ruleName in rulesToRemove)
            {
                try
                {
                    firewallPolicy.Rules.Remove(ruleName);
                    Logger.Log($"Usunięto regułę: {ruleName}");
                    removedCount++;
                }
                catch (Exception ex)
                {
                    Logger.Log($"Błąd podczas usuwania reguły {ruleName}: {ex.Message}");
                }
            }
            
            Logger.Log($"Usunięto {removedCount} z {rulesToRemove.Count} reguł zapory");
            
            return removedCount > 0 || rulesToRemove.Count == 0;
        }
        catch (Exception ex)
        {
            Logger.Log($"Krytyczny błąd przy usuwaniu wszystkich reguł: {ex.Message}\n{ex.StackTrace}");
            return false;
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
    
    private bool AddFirewallRule(string domain, string ipAddress)
    {
        try
        {
            // Utwórz instancję polityki zapory
            Type netFwPolicy2Type = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(netFwPolicy2Type);
            
            // Nazwy reguł
            string outboundRuleName = $"{RULE_PREFIX}{domain}-Out";
            string inboundRuleName = $"{RULE_PREFIX}{domain}-In";
            
            // Utwórz instancje reguł
            Type ruleType = Type.GetTypeFromProgID("HNetCfg.FwRule");
            
            // Reguła dla ruchu wychodzącego
            INetFwRule outboundRule = (INetFwRule)Activator.CreateInstance(ruleType);
            outboundRule.Name = outboundRuleName;
            outboundRule.Description = $"Blokada dostępu do {domain} [{ipAddress}]";
            outboundRule.Action = NET_FW_ACTION_.NET_FW_ACTION_BLOCK;
            outboundRule.Direction = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_OUT;
            outboundRule.Enabled = true;
            outboundRule.InterfaceTypes = "All";
            outboundRule.RemoteAddresses = ipAddress;
            
            // Dodaj regułę do kolekcji reguł zapory
            firewallPolicy.Rules.Add(outboundRule);
            Logger.Log($"Dodano regułę wychodzącą: {outboundRuleName}");
            
            // Reguła dla ruchu przychodzącego (opcjonalnie)
            INetFwRule inboundRule = (INetFwRule)Activator.CreateInstance(ruleType);
            inboundRule.Name = inboundRuleName;
            inboundRule.Description = $"Blokada dostępu od {domain} [{ipAddress}]";
            inboundRule.Action = NET_FW_ACTION_.NET_FW_ACTION_BLOCK;
            inboundRule.Direction = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN;
            inboundRule.Enabled = true;
            inboundRule.InterfaceTypes = "All";
            inboundRule.RemoteAddresses = ipAddress;
            
            // Dodaj regułę do kolekcji reguł zapory
            firewallPolicy.Rules.Add(inboundRule);
            Logger.Log($"Dodano regułę przychodzącą: {inboundRuleName}");
            
            return true;
        }
        catch (Exception ex)
        {
            Logger.Log($"Błąd podczas tworzenia reguły zapory: {ex.Message}");
            return false;
        }
    }
    
    private bool RemoveFirewallRules(string ruleNamePrefix)
    {
        try
        {
            Logger.Log($"Usuwam reguły zapory dla {ruleNamePrefix}");
            
            // Utwórz instancję polityki zapory
            Type netFwPolicy2Type = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(netFwPolicy2Type);
            
            // Znajdź i usuń wszystkie reguły z danym prefiksem
            List<string> rulesToRemove = new List<string>();
            
            // Najpierw zbieramy nazwy wszystkich reguł do usunięcia
            foreach (INetFwRule rule in firewallPolicy.Rules)
            {
                if (rule.Name != null && rule.Name.StartsWith(ruleNamePrefix))
                {
                    Logger.Log($"Znaleziono regułę do usunięcia: {rule.Name}");
                    rulesToRemove.Add(rule.Name);
                }
            }
            
            // Teraz usuwamy wszystkie znalezione reguły
            int removedCount = 0;
            foreach (string ruleName in rulesToRemove)
            {
                try
                {
                    firewallPolicy.Rules.Remove(ruleName);
                    Logger.Log($"Usunięto regułę: {ruleName}");
                    removedCount++;
                }
                catch (Exception ex)
                {
                    Logger.Log($"Błąd podczas usuwania reguły {ruleName}: {ex.Message}");
                }
            }
            
            return removedCount > 0 || rulesToRemove.Count == 0;
        }
        catch (Exception ex)
        {
            Logger.Log($"Błąd podczas usuwania reguł zapory: {ex.Message}");
            return false;
        }
    }
}