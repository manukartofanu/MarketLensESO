using System.Collections.Generic;
using System.Linq;
using System.Windows;
using MarketLensESO.Models;

namespace MarketLensESO
{
    public partial class ItemDetailsWindow : Window
    {
        public ItemDetailsWindow(Item item, List<ItemSale> sales)
        {
            InitializeComponent();
            
            // Set item info
            ItemLinkText.Text = item.ItemLink;
            SalesCountText.Text = item.TotalSalesCount.ToString("N0");
            TotalValueText.Text = item.TotalValueSold.ToString("N0");
            AvgPriceText.Text = item.AveragePrice.ToString("N0");
            
            // Set sales data
            SalesDataGrid.ItemsSource = sales.OrderByDescending(s => s.SaleTimestamp).ToList();
        }
    }
}

