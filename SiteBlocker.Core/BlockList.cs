using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SiteBlocker.Core
{
    public class BlockList
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "New List";
        public List<string> Sites { get; set; } = new List<string>();
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime ModifiedDate { get; set; } = DateTime.Now;
        public bool IsBuiltIn { get; set; } = false;
        
        // Default constructor for JSON deserialization
        public BlockList()
        {
        }
        
        // Constructor with parameters
        public BlockList(string name, List<string> sites, bool isBuiltIn = false)
        {
            Name = name;
            Sites = new List<string>(sites); // Create a copy of the list
            IsBuiltIn = isBuiltIn;
        }
        
        // For display in UI
        public override string ToString()
        {
            return $"{Name} ({Sites.Count} sites)";
        }
    }
}