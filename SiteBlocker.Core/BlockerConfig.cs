using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SiteBlocker.Core;

public class BlockerConfig
{
    public List<string> BlockedSites { get; set; } = new List<string>();
    public List<ScheduleItem> BlockingSchedule { get; set; } = new List<ScheduleItem>();
    public List<BlockList> BlockLists { get; set; } = new List<BlockList>();
    public List<BlockSession> BlockSessions { get; set; } = new List<BlockSession>();

    public string PasswordHash { get; set; }

    public bool IsActive { get; set; }

    public int MaxBlockingHours { get; set; } = 8;
    
    public DateTime? BlockingStartTime { get; set; }
    public TimeSpan MaxBlockingDuration { get; set; } = TimeSpan.FromMinutes(5);
    public bool IsEmergencyModeEnabled { get; set; }

    public static string DefaultConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SiteBlocker", 
        "config.json");

    public void SaveToFile(string path)
    {
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        
        // W wersji produkcyjnej dodamy szyfrowanie:
        // byte[] encryptedData = EncryptionHelper.Encrypt(json);
        // File.WriteAllBytes(path, encryptedData);
        
        // Na razie zapisujemy jako zwykły tekst dla uproszczenia
        File.WriteAllText(path, json);
    }

    public static BlockerConfig LoadFromFile(string path)
    {
        if (!File.Exists(path))
            return new BlockerConfig();

        try
        {
            string json = File.ReadAllText(path);
            
            return JsonSerializer.Deserialize<BlockerConfig>(json) ?? new BlockerConfig();
            
            // W wersji produkcyjnej dodamy deszyfrowanie:
            // byte[] encryptedData = File.ReadAllBytes(path);
            // string json = EncryptionHelper.Decrypt(encryptedData);
            // return JsonSerializer.Deserialize<BlockerConfig>(json);
        }
        catch (Exception)
        {
            return new BlockerConfig();
        }
    }
    
    public bool ShouldBeActiveNow()
    {
        if (IsEmergencyModeEnabled)
            return false;

        if (!IsActive)
            return false;

        if (BlockingStartTime.HasValue)
        {
            TimeSpan elapsed = DateTime.Now - BlockingStartTime.Value;
            if (elapsed > MaxBlockingDuration)
                return false;
        }

        return IsTimeInSchedule();
    }

    private bool IsTimeInSchedule()
    {
        if (BlockingSchedule == null || BlockingSchedule.Count == 0)
            return true;

        foreach (var item in BlockingSchedule)
        {
            if (item.IsActiveNow())
                return true;
        }
    
        return false;
    }

    public void EnableBlocking()
    {
        IsActive = true;
        BlockingStartTime = DateTime.Now;
        IsEmergencyModeEnabled = false;
    }

    public void DisableBlocking()
    {
        IsActive = false;
    }

    public void EnableEmergencyMode()
    {
        IsEmergencyModeEnabled = true;
    }
    
    public bool VerifyPassword(string password)
    {
        if (string.IsNullOrEmpty(PasswordHash))
            return true; // No password set
        
        return BCrypt.Net.BCrypt.Verify(password, PasswordHash);
    }

    public void SetPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            PasswordHash = null;
        else
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
    }
    
    // Get a block list by ID
public BlockList GetBlockListById(string id)
{
    return BlockLists.FirstOrDefault(list => list.Id == id);
}

// Get all blocked sites for a session
public List<string> GetAllBlockedSitesForSession(BlockSession session)
{
    HashSet<string> allSites = new HashSet<string>();
    
    foreach (string listId in session.BlockListIds)
    {
        BlockList list = GetBlockListById(listId);
        if (list != null)
        {
            foreach (string site in list.Sites)
            {
                allSites.Add(site);
            }
        }
    }
    
    return allSites.ToList();
}

// Add built-in preset categories
public void AddBuiltInCategories()
{
    // Only add if they don't exist yet
    if (BlockLists.Any(l => l.IsBuiltIn))
        return;
        
    // Social Media
    BlockLists.Add(new BlockList(
        "Social Media", 
        new List<string> { 
            "facebook.com", "twitter.com", "instagram.com", "tiktok.com", 
            "snapchat.com", "reddit.com", "pinterest.com", "linkedin.com" 
        }, 
        true));
        
    // News Sites
    BlockLists.Add(new BlockList(
        "News", 
        new List<string> { 
            "cnn.com", "bbc.com", "foxnews.com", "nytimes.com", 
            "theguardian.com", "washingtonpost.com", "huffpost.com", "news.yahoo.com" 
        }, 
        true));
        
    // Video Streaming
    BlockLists.Add(new BlockList(
        "Video Streaming", 
        new List<string> { 
            "youtube.com", "netflix.com", "hulu.com", "disney.com", 
            "twitch.tv", "vimeo.com", "dailymotion.com", "hbomax.com" 
        }, 
        true));
        
    // Search Engines
    BlockLists.Add(new BlockList(
        "Search Engines", 
        new List<string> { 
            "google.com", "bing.com", "yahoo.com", "duckduckgo.com", 
            "baidu.com", "yandex.com", "ask.com", "aol.com" 
        }, 
        true));
        
    // Adult Content
    BlockLists.Add(new BlockList(
        "Adult Content", 
        new List<string> { 
            "pornhub.com", "xvideos.com", "xnxx.com", "xhamster.com", 
            "onlyfans.com", "redtube.com", "youporn.com", "chaturbate.com",
            "livejasmin.com"
        }, 
        true));
}
}