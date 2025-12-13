using System;

namespace MarketLensESO.Models
{
    public class GuildSalesByWeek
    {
        public string GuildName { get; set; } = string.Empty;
        public int GuildId { get; set; }
        public DateTime WeekStartDate { get; set; }
        public DateTime WeekEndDate { get; set; }
        public int WeekNumber { get; set; }
        public long TotalSales { get; set; }
        
        public string WeekDisplay => $"Week {WeekStartDate:dd.MM.yy} - {WeekEndDate:dd.MM.yy}";
        public string DateRange => $"{WeekStartDate:dd.MM.yy} to {WeekEndDate:dd.MM.yy}";
    }
}


