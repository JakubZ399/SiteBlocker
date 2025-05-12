using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace SiteBlocker.Core
{
    public class BlockSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Blocking Session";
        public List<string> BlockListIds { get; set; } = new List<string>();
        public DateTime StartTime { get; set; } = DateTime.Now;
        public TimeSpan Duration { get; set; } = TimeSpan.FromHours(2);
        public bool IsActive { get; set; } = true;
        
        // For scheduled sessions
        public bool IsRecurring { get; set; } = false;
        public List<DayOfWeek> RecurringDays { get; set; } = new List<DayOfWeek>();
        public TimeSpan StartTimeOfDay { get; set; } = TimeSpan.Zero;
        public TimeSpan EndTimeOfDay { get; set; } = TimeSpan.Zero;
        
        // Default constructor for JSON deserialization
        public BlockSession()
        {
        }
        
        // Constructor with parameters
        public BlockSession(string name, List<string> blockListIds, TimeSpan duration)
        {
            Name = name;
            BlockListIds = new List<string>(blockListIds);
            Duration = duration;
        }
        
        // Calculate end time
        [JsonIgnore]
        public DateTime EndTime => StartTime + Duration;
        
        // Calculate remaining time
        [JsonIgnore]
        public TimeSpan RemainingTime
        {
            get
            {
                if (!IsActive)
                    return TimeSpan.Zero;
                    
                TimeSpan remaining = EndTime - DateTime.Now;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }
        
        // Check if session is expired
        [JsonIgnore]
        public bool IsExpired => RemainingTime <= TimeSpan.Zero;
        
        // Check if session should be active right now
        public bool ShouldBeActiveNow()
        {
            if (!IsActive)
                return false;
                
            if (!IsRecurring)
                return !IsExpired;
                
            // Check if current time falls within scheduled time
            DateTime now = DateTime.Now;
            TimeSpan currentTimeOfDay = new TimeSpan(now.Hour, now.Minute, now.Second);
            
            return RecurringDays.Contains(now.DayOfWeek) && 
                   currentTimeOfDay >= StartTimeOfDay && 
                   currentTimeOfDay <= EndTimeOfDay;
        }
        
        // Helper to display recurring days in a readable format
        [JsonIgnore]
        public string RecurringDaysDisplay
        {
            get
            {
                if (!IsRecurring || RecurringDays.Count == 0)
                    return string.Empty;
                    
                if (RecurringDays.Count == 7)
                    return "Every day";
                    
                if (RecurringDays.Count == 5 && 
                    RecurringDays.Contains(DayOfWeek.Monday) &&
                    RecurringDays.Contains(DayOfWeek.Tuesday) &&
                    RecurringDays.Contains(DayOfWeek.Wednesday) &&
                    RecurringDays.Contains(DayOfWeek.Thursday) &&
                    RecurringDays.Contains(DayOfWeek.Friday))
                    return "Weekdays";
                    
                if (RecurringDays.Count == 2 && 
                    RecurringDays.Contains(DayOfWeek.Saturday) &&
                    RecurringDays.Contains(DayOfWeek.Sunday))
                    return "Weekends";
                    
                return string.Join(", ", RecurringDays);
            }
        }
    }
}