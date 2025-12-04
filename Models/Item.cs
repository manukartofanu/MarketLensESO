using System;
using System.Collections.Generic;

namespace MarketLensESO.Models
{
    public class Item
    {
        public long ItemId { get; set; }
        public string ItemLink { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        
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

