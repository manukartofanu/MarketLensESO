using System;
using System.Collections.Generic;

namespace MarketLensESO.Models
{
    public class Item
    {
        public long ItemId { get; set; }
        public string ItemLink { get; set; } = string.Empty;
        public long FirstSeenDate { get; set; }
        public long LastSeenDate { get; set; }
        
        // Calculated properties
        public DateTime FirstSeenDateTime => DateTimeOffset.FromUnixTimeSeconds(FirstSeenDate).DateTime;
        public DateTime LastSeenDateTime => DateTimeOffset.FromUnixTimeSeconds(LastSeenDate).DateTime;
        
        // Aggregated data
        public int TotalSalesCount { get; set; }
        public long TotalQuantitySold { get; set; }
        public long TotalValueSold { get; set; }
        public long AveragePrice { get; set; }
        public long MinPrice { get; set; }
        public long MaxPrice { get; set; }
        public List<ItemSale> Sales { get; set; } = new List<ItemSale>();
    }
}

