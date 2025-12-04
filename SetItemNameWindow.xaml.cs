using System;
using System.Windows;

namespace MarketLensESO
{
    public partial class SetItemNameWindow : Window
    {
        public string ItemLink { get; private set; } = string.Empty;
        public string ItemName { get; private set; } = string.Empty;
        public bool Saved { get; private set; }

        public SetItemNameWindow(string itemLink, string currentName = "")
        {
            InitializeComponent();
            ItemLink = itemLink;
            Saved = false;
            
            ItemLinkTextBlock.Text = itemLink;
            ItemNameTextBox.Text = currentName;
            
            // Set the button state
            if (SaveButton != null)
            {
                SaveButton.IsEnabled = true; // Always enabled for text input
            }
        }

        private void ItemNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Button is always enabled for text input
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ItemName = ItemNameTextBox.Text?.Trim() ?? "";
            Saved = true;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

