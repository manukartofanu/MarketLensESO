using System;
using System.Collections.Generic;
using System.Linq;
using MarketLensESO.Models;

namespace MarketLensESO.Services
{
    public class TradingWeekCalculator
    {
        /// <summary>
        /// Gets the trading week number for a given date relative to the current time.
        /// Week 0 is the current trading week, Week -1 is the previous week, etc.
        /// Trading weeks run from Tuesday 14:00 to Tuesday 14:00.
        /// </summary>
        public int GetTradingWeekNumber(DateTime saleDate, DateTime currentTime)
        {
            // Get the current trading week start (Tuesday 14:00)
            var currentWeekStart = GetCurrentTradingWeekStart(currentTime);
            
            // Calculate the difference in days
            var daysDiff = (saleDate - currentWeekStart).TotalDays;
            
            // Calculate which week this sale belongs to
            var weekNumber = (int)Math.Floor(daysDiff / 7.0);
            
            return weekNumber;
        }

        /// <summary>
        /// Gets the start date of the current trading week (Tuesday 14:00).
        /// </summary>
        private DateTime GetCurrentTradingWeekStart(DateTime dateTime)
        {
            // Find the most recent Tuesday at 14:00
            var daysSinceTuesday = ((int)dateTime.DayOfWeek - (int)DayOfWeek.Tuesday + 7) % 7;
            var tuesday = dateTime.Date.AddDays(-daysSinceTuesday).AddHours(14);
            
            // If the current time is before Tuesday 14:00, go back one week
            if (dateTime < tuesday)
            {
                tuesday = tuesday.AddDays(-7);
            }
            
            return tuesday;
        }

        /// <summary>
        /// Gets the start and end dates for a specific trading week number.
        /// </summary>
        public (DateTime Start, DateTime End) GetTradingWeekDates(int weekNumber, DateTime currentTime)
        {
            var currentWeekStart = GetCurrentTradingWeekStart(currentTime);
            var weekStart = currentWeekStart.AddDays(weekNumber * 7);
            var weekEnd = weekStart.AddDays(7);
            
            return (weekStart, weekEnd);
        }

        /// <summary>
        /// Groups sales by trading week and guild, calculating total sales for each combination.
        /// </summary>
        public List<GuildSalesByWeek> CalculateGuildSalesByWeek(List<ItemSale> sales)
        {
            var result = new Dictionary<(int GuildId, int WeekNumber), GuildSalesByWeek>();
            var now = DateTime.Now;

            foreach (var sale in sales)
            {
                var weekNumber = GetTradingWeekNumber(sale.SaleDate, now);
                var key = (sale.GuildId, weekNumber);

                if (!result.ContainsKey(key))
                {
                    var weekDates = GetTradingWeekDates(weekNumber, now);
                    result[key] = new GuildSalesByWeek
                    {
                        GuildId = sale.GuildId,
                        GuildName = sale.GuildName,
                        WeekNumber = weekNumber,
                        WeekStartDate = weekDates.Start,
                        WeekEndDate = weekDates.End,
                        TotalSales = 0
                    };
                }

                result[key].TotalSales += sale.TotalValue;
            }

            // Sort by week start date (newest first), then by guild name
            return result.Values
                .OrderByDescending(g => g.WeekStartDate)
                .ThenBy(g => g.GuildName)
                .ToList();
        }

        /// <summary>
        /// Groups sales by trading week, guild, and item, calculating total sales for each combination.
        /// </summary>
        public List<GuildItemSalesByWeek> CalculateGuildItemSalesByWeek(List<ItemSale> sales, Dictionary<long, string> itemNames)
        {
            var result = new Dictionary<(int GuildId, long ItemId, int WeekNumber), GuildItemSalesByWeek>();
            var now = DateTime.Now;

            foreach (var sale in sales)
            {
                var weekNumber = GetTradingWeekNumber(sale.SaleDate, now);
                var key = (sale.GuildId, sale.ItemId, weekNumber);

                if (!result.ContainsKey(key))
                {
                    result[key] = new GuildItemSalesByWeek
                    {
                        GuildId = sale.GuildId,
                        GuildName = sale.GuildName,
                        ItemId = sale.ItemId,
                        ItemLink = sale.ItemLink,
                        ItemName = itemNames.TryGetValue(sale.ItemId, out var name) ? name : string.Empty,
                        WeekNumber = weekNumber,
                        TotalSales = 0,
                        SalesCount = 0,
                        TotalQuantitySold = 0
                    };
                }

                result[key].TotalSales += sale.TotalValue;
                result[key].SalesCount++;
                result[key].TotalQuantitySold += sale.Quantity;
            }

            // Sort by guild name, then by item link
            return result.Values
                .OrderBy(g => g.GuildName)
                .ThenBy(g => g.ItemLink)
                .ToList();
        }
    }
}

