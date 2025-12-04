using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using MarketLensESO.Models;
using MarketLensESO.Services;

namespace MarketLensESO
{
    public partial class DatabaseWindow : Window
    {
        private readonly DatabaseService _databaseService;
        private readonly LuaFileParser _luaFileParser;

        public DatabaseWindow(DatabaseService databaseService)
        {
            InitializeComponent();
            _databaseService = databaseService;
            _luaFileParser = new LuaFileParser();
            Loaded += DatabaseWindow_Loaded;
        }

        private async void DatabaseWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await UpdateStatisticsAsync();
        }

        private async Task UpdateStatisticsAsync()
        {
            try
            {
                var totalItems = await _databaseService.GetTotalItemsCountAsync();
                var totalSales = await _databaseService.GetTotalSalesCountAsync();
                
                ItemsCountLabel.Text = totalItems.ToString("N0");
                SalesCountLabel.Text = totalSales.ToString("N0");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading statistics: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "Lua files (*.lua)|*.lua|All files (*.*)|*.*",
                    Title = "Select Lua File to Import"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    await ImportFileAsync(openFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing file: {ex.Message}", "Import Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ImportFileAsync(string filePath)
        {
            try
            {
                // Disable import button and show progress
                ImportButton.IsEnabled = false;
                ImportProgressBar.Visibility = Visibility.Visible;
                ImportProgressBar.IsIndeterminate = true;
                ImportStatusText.Text = "Parsing file...";

                // Parse file on background thread
                var sales = await Task.Run(() => _luaFileParser.ParseLuaFile(filePath));

                if (sales.Count == 0)
                {
                    ImportStatusText.Text = "No sales found in file.";
                    ImportProgressBar.Visibility = Visibility.Collapsed;
                    ImportButton.IsEnabled = true;
                    return;
                }

                ImportStatusText.Text = $"Found {sales.Count:N0} sales. Importing to database...";

                // Import to database
                await _databaseService.ImportSalesAsync(sales);

                // Update statistics
                await UpdateStatisticsAsync();

                ImportStatusText.Text = $"Successfully imported {sales.Count:N0} sales from {Path.GetFileName(filePath)}";
                ImportProgressBar.Visibility = Visibility.Collapsed;
                ImportButton.IsEnabled = true;

                MessageBox.Show(
                    $"Import completed successfully!\n\n" +
                    $"Sales processed: {sales.Count:N0}\n" +
                    $"Note: Duplicates are automatically skipped.",
                    "Import Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ImportStatusText.Text = $"Error: {ex.Message}";
                ImportProgressBar.Visibility = Visibility.Collapsed;
                ImportButton.IsEnabled = true;
                MessageBox.Show($"Error importing file: {ex.Message}", "Import Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

