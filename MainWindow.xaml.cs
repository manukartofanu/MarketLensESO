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
        private List<GuildItemSummary> _guildItems;
        private Item? _selectedItem;
        private int? _selectedGuildId;
        private bool _guildItemsLoaded = false;

        public MainWindow()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            _items = new List<Item>();
            _summaries = new List<ItemSummary>();
            _guildItems = new List<GuildItemSummary>();
            Loaded += Window_Loaded;
            MainTabControl.SelectionChanged += MainTabControl_SelectionChanged;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadGuildsAsync();
            await LoadItemsAsync();
            await LoadGuildItemsAsync();
        }

        private async void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Only reload if actually switching tabs (not just clicking within the same tab)
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is TabItem selectedTab)
            {
                var tabHeader = selectedTab.Header?.ToString();
                if (tabHeader == "By Guild")
                {
                    GuildComboBox.IsEnabled = false;
                    if (!_guildItemsLoaded)
                    {
                        await LoadGuildItemsAsync();
                        _guildItemsLoaded = true;
                    }
                }
                else
                {
                    GuildComboBox.IsEnabled = true;
                    if (tabHeader != "By Guild")
                    {
                        _guildItemsLoaded = false;
                    }
                }
            }
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

        private void UpdateSummary()
        {
            try
            {
                // Calculate from filtered items to respect current filters (guild, search)
                var filteredItems = ApplySearchFilter(_items);
                var totalItems = filteredItems.Count;
                var totalSalesValue = filteredItems.Sum(i => i.TotalValueSold);
                
                SummaryText.Text = $"Total Items: {totalItems:N0} | Total Sales Value: {totalSalesValue:N0}";
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
            UpdateGuildItemsDataGrid();
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

        private async Task LoadGuildItemsAsync()
        {
            try
            {
                var allGuildItems = await _databaseService.LoadItemsByGuildAsync();
                
                // Filter: remove entries with less than 100,000 total value
                var filteredItems = allGuildItems
                    .Where(g => g.TotalValueSold >= 100000)
                    .ToList();
                
                // Calculate Internal counts
                var internalCounts = await _databaseService.CalculateInternalCountsAsync(filteredItems);
                
                // Set Internal count for each item
                foreach (var item in filteredItems)
                {
                    if (internalCounts.TryGetValue((item.ItemId, item.GuildId), out var count))
                    {
                        item.Internal = count;
                    }
                }
                
                // Sort by total value descending
                _guildItems = filteredItems
                    .OrderByDescending(g => g.TotalValueSold)
                    .ToList();
                
                UpdateGuildItemsDataGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading guild items: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateGuildItemsDataGrid()
        {
            var filteredGuildItems = ApplyGuildItemsSearchFilter(_guildItems);
            GuildItemsDataGrid.ItemsSource = filteredGuildItems;
        }

        private List<GuildItemSummary> ApplyGuildItemsSearchFilter(List<GuildItemSummary> guildItems)
        {
            if (string.IsNullOrWhiteSpace(SearchTextBox?.Text))
                return guildItems;

            var searchText = SearchTextBox.Text.ToLowerInvariant();
            return guildItems.Where(g =>
                (g.ItemLink?.ToLowerInvariant().Contains(searchText) ?? false) ||
                (g.Name?.ToLowerInvariant().Contains(searchText) ?? false) ||
                (g.GuildName?.ToLowerInvariant().Contains(searchText) ?? false) ||
                g.TotalValueSold.ToString().Contains(searchText) ||
                g.TotalSalesCount.ToString().Contains(searchText) ||
                g.TotalQuantitySold.ToString().Contains(searchText)
            ).ToList();
        }

        private void GuildItemsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Do nothing - prevent any refresh on selection change
        }

        private async void ViewDetailsGuildMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (GuildItemsDataGrid.SelectedItem is GuildItemSummary selectedGuildItem)
                {
                    var sales = await _databaseService.LoadSalesForItemInGuildAsync(
                        selectedGuildItem.ItemId, 
                        selectedGuildItem.GuildId);
                    
                    // Get all sellers for this guild
                    var sellers = await _databaseService.GetAllSellersInGuildAsync(selectedGuildItem.GuildId);
                    
                    var detailsWindow = new GuildItemDetailsWindow(selectedGuildItem, sales, sellers);
                    detailsWindow.Owner = this;
                    detailsWindow.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading item details: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

