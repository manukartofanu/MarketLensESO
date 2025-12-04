using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MarketLensESO.Models;
using MarketLensESO.Services;

namespace MarketLensESO
{
    public partial class MainWindow : Window
    {
        private readonly DatabaseService _databaseService;
        private List<Item> _items;
        private Item? _selectedItem;

        public MainWindow()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            _items = new List<Item>();
            Loaded += Window_Loaded;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadItemsAsync();
        }

        private async Task LoadItemsAsync()
        {
            try
            {
                ItemsDataGrid.ItemsSource = null;
                _items = await _databaseService.LoadAllItemsAsync();
                UpdateDataGrid();
                UpdateSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading items: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateDataGrid()
        {
            var filteredItems = ApplySearchFilter(_items);
            ItemsDataGrid.ItemsSource = filteredItems;
        }

        private List<Item> ApplySearchFilter(List<Item> items)
        {
            if (string.IsNullOrWhiteSpace(SearchTextBox?.Text))
                return items;

            var searchText = SearchTextBox.Text.ToLowerInvariant();
            return items.Where(i =>
                (i.ItemLink?.ToLowerInvariant().Contains(searchText) ?? false) ||
                i.TotalSalesCount.ToString().Contains(searchText) ||
                i.TotalQuantitySold.ToString().Contains(searchText) ||
                i.TotalValueSold.ToString().Contains(searchText) ||
                i.AveragePrice.ToString().Contains(searchText) ||
                i.MinPrice.ToString().Contains(searchText) ||
                i.MaxPrice.ToString().Contains(searchText)
            ).ToList();
        }

        private async void UpdateSummary()
        {
            try
            {
                var totalItems = await _databaseService.GetTotalItemsCountAsync();
                var totalSales = await _databaseService.GetTotalSalesCountAsync();
                SummaryText.Text = $"Total Items: {totalItems:N0} | Total Sales: {totalSales:N0}";
            }
            catch (Exception ex)
            {
                SummaryText.Text = "Error loading summary";
                System.Diagnostics.Debug.WriteLine($"Error updating summary: {ex.Message}");
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateDataGrid();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadItemsAsync();
        }

        private async void DatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var databaseWindow = new DatabaseWindow(_databaseService)
                {
                    Owner = this
                };
                databaseWindow.ShowDialog();
                
                // Refresh data after import
                await LoadItemsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening database window: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ItemsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedItem = ItemsDataGrid.SelectedItem as Item;
        }

        private async void ItemsDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_selectedItem == null)
                return;

            try
            {
                // Load sales for this item
                var sales = await _databaseService.LoadSalesForItemAsync(_selectedItem.ItemId);
                
                // Show item details window
                var detailsWindow = new ItemDetailsWindow(_selectedItem, sales)
                {
                    Owner = this
                };
                detailsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading item details: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

