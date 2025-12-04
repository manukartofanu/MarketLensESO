using System;

namespace MarketLensESO.Models
{
    public class ItemSale
    {
        public long SaleId { get; set; }
        public long ItemId { get; set; }
        public string ItemLink { get; set; } = string.Empty;
        public int GuildId { get; set; }
        public string GuildName { get; set; } = string.Empty;
        public string Seller { get; set; } = string.Empty;
        public string Buyer { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int Price { get; set; }
        public long SaleTimestamp { get; set; }
        public int DuplicateIndex { get; set; } = 1;
        
        // Calculated properties
        public DateTime SaleDate => DateTimeOffset.FromUnixTimeSeconds(SaleTimestamp).DateTime;
        public long TotalValue => (long)Price * Quantity;
    }
}

