namespace MarketLensESO.Models
{
    public class GuildItemSummary
    {
        public long ItemId { get; set; }
        public string ItemLink { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int GuildId { get; set; }
        public string GuildName { get; set; } = string.Empty;
        public long TotalValueSold { get; set; }
        public int TotalSalesCount { get; set; }
        public long TotalQuantitySold { get; set; }
        
        public int Internal { get; set; }
        
        // Calculated properties
        public long Percent3_5 => (long)(TotalValueSold * 0.035);
        public long Percent1 => (long)(TotalValueSold * 0.01);
    }
}

