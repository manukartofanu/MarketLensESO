using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MarketLensESO.Models;

namespace MarketLensESO.Services
{
    public class LuaFileParser
    {
        // Track duplicate counters for each file parsing session
        private Dictionary<string, int> _saleDuplicateCounters = new();

        public List<ItemSale> ParseLuaFile(string filePath)
        {
            var sales = new List<ItemSale>();
            
            if (!File.Exists(filePath))
                return sales;

            try
            {
                // Reset duplicate counters for new file
                _saleDuplicateCounters.Clear();
                
                string content = File.ReadAllText(filePath);

                // Parse the format: guilds section with sales
                foreach (var guildEntry in ExtractGuildEntries(content))
                {
                    // Parse sales
                    foreach (var saleBlock in ExtractSingleSaleBlocks(guildEntry.SalesInner))
                    {
                        var sale = ParseSale(saleBlock);
                        if (sale != null)
                        {
                            sale.GuildId = guildEntry.GuildId;
                            sale.GuildName = guildEntry.GuildName;
                            sales.Add(sale);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error parsing Lua file: {ex.Message}", ex);
            }

            return sales;
        }

        private ItemSale? ParseSale(string saleData)
        {
            try
            {
                var sale = new ItemSale();

                // New compressed format: l = itemLink, n = quantity, p = price, s = seller, b = buyer, ts = timestamp
                // Extract item link (l)
                var itemLinkMatch = Regex.Match(saleData, @"\[""l""\]\s*=\s*""([^""]+)""");
                if (itemLinkMatch.Success)
                {
                    sale.ItemLink = itemLinkMatch.Groups[1].Value;
                }

                // Extract buyer (b)
                var buyerMatch = Regex.Match(saleData, @"\[""b""\]\s*=\s*""([^""]+)""");
                if (buyerMatch.Success)
                {
                    sale.Buyer = buyerMatch.Groups[1].Value;
                }

                // Extract seller (s)
                var sellerMatch = Regex.Match(saleData, @"\[""s""\]\s*=\s*""([^""]+)""");
                if (sellerMatch.Success)
                {
                    sale.Seller = sellerMatch.Groups[1].Value;
                }

                // Extract quantity (n)
                var quantityMatch = Regex.Match(saleData, @"\[""n""\]\s*=\s*(\d+)");
                if (quantityMatch.Success)
                {
                    sale.Quantity = int.Parse(quantityMatch.Groups[1].Value);
                }

                // Extract price (p)
                var priceMatch = Regex.Match(saleData, @"\[""p""\]\s*=\s*(\d+)");
                if (priceMatch.Success)
                {
                    sale.Price = int.Parse(priceMatch.Groups[1].Value);
                }

                // Extract timestamp (ts)
                var timestampMatch = Regex.Match(saleData, @"\[""ts""\]\s*=\s*(\d+)");
                if (timestampMatch.Success)
                {
                    sale.SaleTimestamp = long.Parse(timestampMatch.Groups[1].Value);
                }

                // Assign duplicate index for duplicate detection
                var duplicateKey = $"{sale.SaleTimestamp}|{sale.Seller}|{sale.Buyer}|{sale.Quantity}|{sale.Price}|{sale.ItemLink}";
                if (!_saleDuplicateCounters.ContainsKey(duplicateKey))
                {
                    _saleDuplicateCounters[duplicateKey] = 0;
                }
                sale.DuplicateIndex = ++_saleDuplicateCounters[duplicateKey];

                return sale;
            }
            catch
            {
                return null;
            }
        }

        private List<(int GuildId, string GuildName, string SalesInner)> ExtractGuildEntries(string content)
        {
            var results = new List<(int, string, string)>();

            // Find the main data structure - look for the first opening brace after ManuGuildHelper_SavedData
            var dataStart = content.IndexOf("ManuGuildHelper_SavedData");
            if (dataStart == -1)
                return results;

            var dataBrace = content.IndexOf('{', dataStart);
            if (dataBrace == -1)
                return results;

            var dataBlock = ExtractBalancedBlock(content, dataBrace);
            var dataInner = TrimOuterBraces(dataBlock);

            // Iterate guild entries: [guildId] = { ... }
            var i = 0;
            while (i < dataInner.Length)
            {
                var keyStart = dataInner.IndexOf('[', i);
                if (keyStart == -1) break;
                var equalsIndex = dataInner.IndexOf('=', keyStart);
                if (equalsIndex == -1) break;
                var braceStart = dataInner.IndexOf('{', equalsIndex);
                if (braceStart == -1) { i = equalsIndex + 1; continue; }

                // Extract guild table block
                var block = ExtractBalancedBlock(dataInner, braceStart);
                if (string.IsNullOrEmpty(block)) { i = braceStart + 1; continue; }
                var inner = TrimOuterBraces(block);

                // Parse guild id from key
                var keyText = dataInner.Substring(keyStart, equalsIndex - keyStart);
                var idMatch = Regex.Match(keyText, @"\[(\d+)\]");
                var guildId = idMatch.Success ? int.Parse(idMatch.Groups[1].Value) : 0;

                // Extract guild name from inner content
                var nameMatch = Regex.Match(inner, "\\[\\\"guildName\\\"\\]\\s*=\\s*\\\"([^\\\"]+)\\\"", RegexOptions.Singleline);
                var guildName = nameMatch.Success ? nameMatch.Groups[1].Value : $"Guild {guildId}";

                // Extract sales inner
                var salesMarker = "[\"sales\"]";
                var salesIndex = inner.IndexOf(salesMarker, StringComparison.Ordinal);
                string salesInner = string.Empty;
                if (salesIndex != -1)
                {
                    var salesBrace = inner.IndexOf('{', salesIndex);
                    if (salesBrace != -1)
                    {
                        var salesBlock = ExtractBalancedBlock(inner, salesBrace);
                        salesInner = TrimOuterBraces(salesBlock);
                    }
                }

                results.Add((guildId, guildName, salesInner));
                i = braceStart + block.Length;
            }

            return results;
        }

        private List<string> ExtractSingleSaleBlocks(string salesBlock)
        {
            var results = new List<string>();

            // Scan for patterns like [123] = { ... }
            var i = 0;
            while (i < salesBlock.Length)
            {
                // Find start of an entry key [
                var keyStart = salesBlock.IndexOf('[', i);
                if (keyStart == -1)
                    break;

                // Find '=' after the key closes
                var equalsIndex = salesBlock.IndexOf('=', keyStart);
                if (equalsIndex == -1)
                    break;

                // Find the opening brace for the value
                var braceStart = salesBlock.IndexOf('{', equalsIndex);
                if (braceStart == -1)
                {
                    i = equalsIndex + 1;
                    continue;
                }

                var block = ExtractBalancedBlock(salesBlock, braceStart);
                if (!string.IsNullOrEmpty(block))
                {
                    var inner = TrimOuterBraces(block);
                    if (!string.IsNullOrWhiteSpace(inner))
                        results.Add(inner);

                    i = braceStart + block.Length; // jump past this block
                }
                else
                {
                    i = braceStart + 1;
                }
            }

            return results;
        }

        private string ExtractBalancedBlock(string text, int openingBraceIndex)
        {
            if (openingBraceIndex < 0 || openingBraceIndex >= text.Length || text[openingBraceIndex] != '{')
                return string.Empty;

            var depth = 0;
            for (int j = openingBraceIndex; j < text.Length; j++)
            {
                if (text[j] == '{') depth++;
                else if (text[j] == '}') depth--;

                if (depth == 0)
                {
                    return text.Substring(openingBraceIndex, j - openingBraceIndex + 1);
                }
            }

            return string.Empty;
        }

        private string TrimOuterBraces(string block)
        {
            block = block.Trim();
            if (block.Length >= 2 && block[0] == '{' && block[block.Length - 1] == '}')
            {
                return block.Substring(1, block.Length - 2);
            }
            return block;
        }
    }
}

