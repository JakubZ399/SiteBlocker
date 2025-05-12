using System;

namespace SiteBlocker.Core;

public class ScheduleItem
{
    // Dzień tygodnia, w którym ma działać blokada
    public DayOfWeek Day { get; set; }
    
    // Czas rozpoczęcia blokady
    public TimeSpan StartTime { get; set; }
    
    // Czas zakończenia blokady
    public TimeSpan EndTime { get; set; }
    
    // Czy ta pozycja harmonogramu jest aktywna
    public bool IsEnabled { get; set; } = true;
    
    // Pomocnicza metoda do sprawdzania, czy bieżący czas pasuje do tego elementu harmonogramu
    public bool IsActiveNow()
    {
        DateTime now = DateTime.Now;
        
        // Sprawdź, czy dzisiaj jest odpowiedni dzień tygodnia
        if (Day != now.DayOfWeek)
            return false;
        
        // Pobierz aktualny czas jako TimeSpan
        TimeSpan currentTime = new TimeSpan(now.Hour, now.Minute, now.Second);
        
        // Sprawdź, czy aktualny czas mieści się w przedziale
        return IsEnabled && currentTime >= StartTime && currentTime <= EndTime;
    }
    
    public override string ToString()
    {
        return $"{Day}: {StartTime.ToString(@"hh\:mm")} - {EndTime.ToString(@"hh\:mm")} ({(IsEnabled ? "Aktywny" : "Nieaktywny")})";
    }
}