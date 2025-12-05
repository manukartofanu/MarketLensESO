using System.Collections.Generic;
using System.Linq;
using System.Windows;
using MarketLensESO.Models;

namespace MarketLensESO
{
    public partial class GuildItemDetailsWindow : Window
    {
        public class QuantityGroup
        {
            public int Quantity { get; set; }
            public int LotsCount { get; set; }
            public long TotalValue { get; set; }
            public long AveragePrice { get; set; }
            
            // Calculated properties
            public long Percent3_5 => (long)(TotalValue * 0.035);
            public long Percent1 => (long)(TotalValue * 0.01);
        }

        public GuildItemDetailsWindow(GuildItemSummary guildItem, List<ItemSale> sales)
        {
            InitializeComponent();
            
            // Set item info
            ItemLinkText.Text = guildItem.ItemLink;
            GuildNameText.Text = guildItem.GuildName;
            SalesCountText.Text = guildItem.TotalSalesCount.ToString("N0");
            TotalValueText.Text = guildItem.TotalValueSold.ToString("N0");
            
            // Group sales by quantity
            var grouped = sales
                .GroupBy(s => s.Quantity)
                .Select(g => new QuantityGroup
                {
                    Quantity = g.Key,
                    LotsCount = g.Count(),
                    TotalValue = g.Sum(s => s.Price),
                    AveragePrice = g.Key > 0 ? (long)(g.Sum(s => s.Price) / (double)(g.Sum(s => s.Quantity))) : 0
                })
                .OrderByDescending(g => g.Quantity)
                .ToList();
            
            // Set grouped data
            GroupedSalesDataGrid.ItemsSource = grouped;
        }
    }
}

