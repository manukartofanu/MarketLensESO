namespace MarketLensESO.Models
{
    public class GuildItemSalesByWeek
    {
        public string GuildName { get; set; } = string.Empty;
        public int GuildId { get; set; }
        public string ItemLink { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public long ItemId { get; set; }
        public int WeekNumber { get; set; }
        public long TotalSales { get; set; }
        public int SalesCount { get; set; }
        public int TotalQuantitySold { get; set; }
    }
}

