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
        private List<ItemSummary> _summaries;
        private Item? _selectedItem;
        private int? _selectedGuildId;

        public MainWindow()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            _items = new List<Item>();
            _summaries = new List<ItemSummary>();
            Loaded += Window_Loaded;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadGuildsAsync();
            await LoadItemsAsync();
        }

        private async Task LoadGuildsAsync()
        {
            try
            {
                var guilds = await _databaseService.LoadAllGuildsAsync();
                
                // Add "All Guilds" option
                var guildItems = new List<KeyValuePair<int?, string>>
                {
                    new KeyValuePair<int?, string>(null, "All Guilds")
                };
                
                foreach (var guild in guilds)
                {
                    guildItems.Add(new KeyValuePair<int?, string>(guild.GuildId, guild.GuildName));
                }
                
                GuildComboBox.ItemsSource = guildItems;
                GuildComboBox.SelectedIndex = 0; // Select "All Guilds" by default
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading guilds: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadItemsAsync()
        {
            try
            {
                ItemsDataGrid.ItemsSource = null;
                SummaryDataGrid.ItemsSource = null;
                _items = await _databaseService.LoadAllItemsAsync(_selectedGuildId);
                _summaries = await _databaseService.LoadItemSummariesAsync(_selectedGuildId);
                UpdateDataGrid();
                UpdateSummaryDataGrid();
                UpdateSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading items: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void GuildComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GuildComboBox.SelectedItem is KeyValuePair<int?, string> selected)
            {
                _selectedGuildId = selected.Key;
            }
            else
            {
                _selectedGuildId = null;
            }
            
            await LoadItemsAsync();
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
                (i.Name?.ToLowerInvariant().Contains(searchText) ?? false) ||
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
            UpdateSummaryDataGrid();
        }

        private void UpdateSummaryDataGrid()
        {
            var filteredSummaries = ApplySummarySearchFilter(_summaries);
            SummaryDataGrid.ItemsSource = filteredSummaries;
        }

        private List<ItemSummary> ApplySummarySearchFilter(List<ItemSummary> summaries)
        {
            if (string.IsNullOrWhiteSpace(SearchTextBox?.Text))
                return summaries;

            var searchText = SearchTextBox.Text.ToLowerInvariant();
            return summaries.Where(s =>
                (s.ItemLink?.ToLowerInvariant().Contains(searchText) ?? false) ||
                (s.Name?.ToLowerInvariant().Contains(searchText) ?? false) ||
                s.TotalSalesCount.ToString().Contains(searchText) ||
                s.TotalQuantitySold.ToString().Contains(searchText) ||
                s.TotalValueSold.ToString().Contains(searchText) ||
                s.AveragePrice.ToString().Contains(searchText) ||
                s.MinPrice.ToString().Contains(searchText) ||
                s.MaxPrice.ToString().Contains(searchText)
            ).ToList();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadGuildsAsync();
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
                
                // Refresh guilds and data after import
                await LoadGuildsAsync();
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

        private void CopyLinkMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SummaryDataGrid.SelectedItem is ItemSummary selectedSummary)
                {
                    Clipboard.SetText(selectedSummary.ItemLink);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error copying link: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SetNameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ItemsDataGrid.SelectedItem is Item selectedItem)
                {
                    var setNameWindow = new SetItemNameWindow(selectedItem.ItemLink, selectedItem.Name);
                    setNameWindow.Owner = this;
                    
                    if (setNameWindow.ShowDialog() == true && setNameWindow.Saved)
                    {
                        await _databaseService.UpdateItemNameAsync(selectedItem.ItemId, setNameWindow.ItemName);
                        await LoadItemsAsync(); // Refresh the data
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error setting item name: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SetNameSummaryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SummaryDataGrid.SelectedItem is ItemSummary selectedSummary)
                {
                    var currentName = await _databaseService.GetItemNameAsync(selectedSummary.ItemId);
                    var setNameWindow = new SetItemNameWindow(selectedSummary.ItemLink, currentName);
                    setNameWindow.Owner = this;
                    
                    if (setNameWindow.ShowDialog() == true && setNameWindow.Saved)
                    {
                        await _databaseService.UpdateItemNameAsync(selectedSummary.ItemId, setNameWindow.ItemName);
                        await LoadItemsAsync(); // Refresh the data
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error setting item name: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

