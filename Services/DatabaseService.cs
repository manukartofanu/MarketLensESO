using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Text;
using MarketLensESO.Models;

namespace MarketLensESO.Services
{
    public class DatabaseService
    {
        private readonly string _databasePath;
        private readonly string _schemaPath;
        private readonly string _connectionString;
        private const int BatchSize = 5000;

        public DatabaseService()
        {
            // Store database in AppData
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "MarketLensESO");
            Directory.CreateDirectory(appFolder);
            _databasePath = Path.Combine(appFolder, "ItemHistory.db");
            
            // Get schema path from the Database folder in the project
            var projectRoot = Directory.GetCurrentDirectory();
            _schemaPath = Path.Combine(projectRoot, "Database", "schema.sql");
            
            // Build connection string
            _connectionString = $"Data Source={_databasePath}";
            
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            EnsureDatabaseExists();
        }

        private void EnsureDatabaseExists()
        {
            try
            {
                var isNewDatabase = !File.Exists(_databasePath);
                
                if (isNewDatabase)
                {
                    var directory = Path.GetDirectoryName(_databasePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                }

                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                ConfigureSqliteOptimizations(connection);
                
                // Check if tables exist, create if they don't
                if (!TablesExist(connection))
                {
                    CreateTables(connection);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to initialize database: {ex.Message}", ex);
            }
        }

        private bool TablesExist(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(*) FROM sqlite_master 
                WHERE type='table' AND name IN ('Items', 'ItemSales')
            ";
            var count = Convert.ToInt32(command.ExecuteScalar());
            return count == 2;
        }

        private void ConfigureSqliteOptimizations(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA cache_size = 10000;
                PRAGMA temp_store = MEMORY;
                PRAGMA mmap_size = 268435456;
            ";
            command.ExecuteNonQuery();
        }

        private void CreateTables(SqliteConnection connection)
        {
            try
            {
                // Use embedded schema to ensure it always works
                CreateTablesDirectly(connection);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create tables: {ex.Message}", ex);
            }
        }

        private void CreateTablesDirectly(SqliteConnection connection)
        {
            // Enable foreign keys
            using (var pragmaCommand = connection.CreateCommand())
            {
                pragmaCommand.CommandText = "PRAGMA foreign_keys = ON";
                pragmaCommand.ExecuteNonQuery();
            }

            // Create Items table
            using (var createItemsCommand = connection.CreateCommand())
            {
                createItemsCommand.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Items (
                        ItemId INTEGER PRIMARY KEY AUTOINCREMENT,
                        ItemLink TEXT NOT NULL UNIQUE,
                        Name TEXT NOT NULL DEFAULT ''
                    )";
                createItemsCommand.ExecuteNonQuery();
            }

            // Create ItemSales table
            using (var createSalesCommand = connection.CreateCommand())
            {
                createSalesCommand.CommandText = @"
                    CREATE TABLE IF NOT EXISTS ItemSales (
                        SaleId INTEGER PRIMARY KEY AUTOINCREMENT,
                        ItemId INTEGER NOT NULL,
                        GuildId INTEGER NOT NULL,
                        GuildName TEXT NOT NULL,
                        Seller TEXT NOT NULL,
                        Buyer TEXT NOT NULL,
                        Quantity INTEGER NOT NULL,
                        Price INTEGER NOT NULL,
                        SaleTimestamp INTEGER NOT NULL,
                        DuplicateIndex TINYINT NOT NULL DEFAULT 1,
                        ContentHash INTEGER NOT NULL UNIQUE,
                        FOREIGN KEY (ItemId) REFERENCES Items(ItemId)
                    )";
                createSalesCommand.ExecuteNonQuery();
            }

            // Create indexes
            var indexStatements = new[]
            {
                "CREATE INDEX IF NOT EXISTS IX_Items_ItemLink ON Items(ItemLink)",
                "CREATE INDEX IF NOT EXISTS IX_ItemSales_ItemId ON ItemSales(ItemId)",
                "CREATE INDEX IF NOT EXISTS IX_ItemSales_GuildId ON ItemSales(GuildId)",
                "CREATE INDEX IF NOT EXISTS IX_ItemSales_Seller ON ItemSales(Seller)",
                "CREATE INDEX IF NOT EXISTS IX_ItemSales_Buyer ON ItemSales(Buyer)",
                "CREATE INDEX IF NOT EXISTS IX_ItemSales_SaleTimestamp ON ItemSales(SaleTimestamp)",
                "CREATE INDEX IF NOT EXISTS IX_ItemSales_ContentHash ON ItemSales(ContentHash)",
                "CREATE INDEX IF NOT EXISTS IX_ItemSales_DuplicateIndex ON ItemSales(DuplicateIndex)"
            };

            foreach (var indexSql in indexStatements)
            {
                using var indexCommand = connection.CreateCommand();
                indexCommand.CommandText = indexSql;
                indexCommand.ExecuteNonQuery();
            }
        }


        public async Task ImportSalesAsync(List<ItemSale> sales)
        {
            await Task.Run(() =>
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                ConfigureSqliteOptimizations(connection);

                using var transaction = connection.BeginTransaction();
                
                try
                {
                    // Group sales by item link to get or create items
                    var salesByLink = sales.GroupBy(s => s.ItemLink).ToList();
                    
                    // Dictionary to cache item IDs
                    var itemIdCache = new Dictionary<string, long>();
                    
                    // Process in batches
                    for (int i = 0; i < salesByLink.Count; i += BatchSize)
                    {
                        var batch = salesByLink.Skip(i).Take(BatchSize).ToList();
                        
                        foreach (var linkGroup in batch)
                        {
                            var itemLink = linkGroup.Key;
                            var firstSale = linkGroup.OrderBy(s => s.SaleTimestamp).First();
                            
                            // Get or create item
                            long itemId;
                            if (!itemIdCache.TryGetValue(itemLink, out itemId))
                            {
                                itemId = GetOrCreateItem(connection, itemLink);
                                itemIdCache[itemLink] = itemId;
                            }
                            
                            // Insert sales for this item
                            foreach (var sale in linkGroup)
                            {
                                InsertSale(connection, itemId, sale);
                            }
                        }
                    }
                    
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            });
        }

        private long GetOrCreateItem(SqliteConnection connection, string itemLink)
        {
            // Try to get existing item
            using var selectCommand = connection.CreateCommand();
            selectCommand.CommandText = "SELECT ItemId FROM Items WHERE ItemLink = @ItemLink";
            selectCommand.Parameters.AddWithValue("@ItemLink", itemLink);
            
            using var reader = selectCommand.ExecuteReader();
            if (reader.Read())
            {
                return reader.GetInt64(0);
            }
            
            // Create new item
            using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
                INSERT INTO Items (ItemLink, Name)
                VALUES (@ItemLink, '');
                SELECT last_insert_rowid();
            ";
            insertCommand.Parameters.AddWithValue("@ItemLink", itemLink);
            
            var result = insertCommand.ExecuteScalar();
            return result != null ? (long)result : 0;
        }

        private void InsertSale(SqliteConnection connection, long itemId, ItemSale sale)
        {
            // Calculate content hash for duplicate detection
            var hashInput = $"{sale.SaleTimestamp}|{sale.Seller}|{sale.Buyer}|{sale.Quantity}|{sale.Price}|{itemId}";
            var hashBytes = XxHash64.Hash(Encoding.UTF8.GetBytes(hashInput));
            var contentHash = BitConverter.ToInt64(hashBytes);
            
            // Check if sale already exists
            using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT COUNT(*) FROM ItemSales WHERE ContentHash = @ContentHash";
            checkCommand.Parameters.AddWithValue("@ContentHash", contentHash);
            var exists = Convert.ToInt32(checkCommand.ExecuteScalar()) > 0;
            
            if (exists)
            {
                return; // Skip duplicate
            }
            
            // Insert sale
            using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
                INSERT INTO ItemSales (ItemId, GuildId, GuildName, Seller, Buyer, Quantity, Price, SaleTimestamp, DuplicateIndex, ContentHash)
                VALUES (@ItemId, @GuildId, @GuildName, @Seller, @Buyer, @Quantity, @Price, @SaleTimestamp, @DuplicateIndex, @ContentHash)
            ";
            insertCommand.Parameters.AddWithValue("@ItemId", itemId);
            insertCommand.Parameters.AddWithValue("@GuildId", sale.GuildId);
            insertCommand.Parameters.AddWithValue("@GuildName", sale.GuildName);
            insertCommand.Parameters.AddWithValue("@Seller", sale.Seller);
            insertCommand.Parameters.AddWithValue("@Buyer", sale.Buyer);
            insertCommand.Parameters.AddWithValue("@Quantity", sale.Quantity);
            insertCommand.Parameters.AddWithValue("@Price", sale.Price);
            insertCommand.Parameters.AddWithValue("@SaleTimestamp", sale.SaleTimestamp);
            insertCommand.Parameters.AddWithValue("@DuplicateIndex", sale.DuplicateIndex);
            insertCommand.Parameters.AddWithValue("@ContentHash", contentHash);
            insertCommand.ExecuteNonQuery();
        }

        public async Task<List<ItemSale>> LoadSalesForItemAsync(long itemId)
        {
            return await Task.Run(() =>
            {
                var sales = new List<ItemSale>();
                
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT 
                        s.SaleId,
                        s.ItemId,
                        i.ItemLink,
                        s.GuildId,
                        s.GuildName,
                        s.Seller,
                        s.Buyer,
                        s.Quantity,
                        s.Price,
                        s.SaleTimestamp,
                        s.DuplicateIndex
                    FROM ItemSales s
                    INNER JOIN Items i ON s.ItemId = i.ItemId
                    WHERE s.ItemId = @ItemId
                    ORDER BY s.SaleTimestamp DESC
                ";
                command.Parameters.AddWithValue("@ItemId", itemId);
                
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    sales.Add(new ItemSale
                    {
                        SaleId = reader.GetInt64(0),
                        ItemId = reader.GetInt64(1),
                        ItemLink = reader.GetString(2),
                        GuildId = reader.GetInt32(3),
                        GuildName = reader.GetString(4),
                        Seller = reader.GetString(5),
                        Buyer = reader.GetString(6),
                        Quantity = reader.GetInt32(7),
                        Price = reader.GetInt32(8),
                        SaleTimestamp = reader.GetInt64(9),
                        DuplicateIndex = reader.GetInt32(10)
                    });
                }
                
                return sales;
            });
        }

        public async Task<List<ItemSale>> LoadAllSalesAsync()
        {
            return await Task.Run(() =>
            {
                var sales = new List<ItemSale>();
                
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT 
                        s.SaleId,
                        s.ItemId,
                        i.ItemLink,
                        s.GuildId,
                        s.GuildName,
                        s.Seller,
                        s.Buyer,
                        s.Quantity,
                        s.Price,
                        s.SaleTimestamp,
                        s.DuplicateIndex
                    FROM ItemSales s
                    INNER JOIN Items i ON s.ItemId = i.ItemId
                    ORDER BY s.SaleTimestamp DESC
                ";
                
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    sales.Add(new ItemSale
                    {
                        SaleId = reader.GetInt64(0),
                        ItemId = reader.GetInt64(1),
                        ItemLink = reader.GetString(2),
                        GuildId = reader.GetInt32(3),
                        GuildName = reader.GetString(4),
                        Seller = reader.GetString(5),
                        Buyer = reader.GetString(6),
                        Quantity = reader.GetInt32(7),
                        Price = reader.GetInt32(8),
                        SaleTimestamp = reader.GetInt64(9),
                        DuplicateIndex = reader.GetInt32(10)
                    });
                }
                
                return sales;
            });
        }

        public async Task<int> GetTotalItemsCountAsync()
        {
            return await Task.Run(() =>
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM Items";
                return Convert.ToInt32(command.ExecuteScalar());
            });
        }

        public async Task<int> GetTotalSalesCountAsync()
        {
            return await Task.Run(() =>
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM ItemSales";
                return Convert.ToInt32(command.ExecuteScalar());
            });
        }

        public async Task<List<ItemSummary>> LoadItemSummariesAsync(int? guildId = null)
        {
            return await Task.Run(() =>
            {
                var summaries = new List<ItemSummary>();
                
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                using var command = connection.CreateCommand();
                var guildFilter = guildId.HasValue ? "AND s.GuildId = @GuildId" : "";
                command.CommandText = $@"
                    SELECT 
                        i.ItemId,
                        i.ItemLink,
                        i.Name,
                        COUNT(s.SaleId) as TotalSalesCount,
                        COALESCE(SUM(s.Quantity), 0) as TotalQuantitySold,
                        COALESCE(SUM(s.Price), 0) as TotalValueSold,
                        CASE 
                            WHEN SUM(s.Quantity) > 0 THEN COALESCE(SUM(s.Price) / CAST(SUM(s.Quantity) AS REAL), 0)
                            ELSE 0
                        END as AveragePrice,
                        COALESCE(MIN(s.Price / CAST(s.Quantity AS REAL)), 0) as MinPrice,
                        COALESCE(MAX(s.Price / CAST(s.Quantity AS REAL)), 0) as MaxPrice
                    FROM Items i
                    INNER JOIN ItemSales s ON i.ItemId = s.ItemId
                    WHERE 1=1 {guildFilter}
                    GROUP BY i.ItemId, i.ItemLink, i.Name
                    ORDER BY TotalValueSold DESC
                ";
                
                if (guildId.HasValue)
                {
                    command.Parameters.AddWithValue("@GuildId", guildId.Value);
                }
                
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    summaries.Add(new ItemSummary
                    {
                        ItemId = reader.GetInt64(0),
                        ItemLink = reader.GetString(1),
                        Name = reader.GetString(2),
                        TotalSalesCount = reader.GetInt32(3),
                        TotalQuantitySold = reader.GetInt64(4),
                        TotalValueSold = reader.GetInt64(5),
                        AveragePrice = reader.GetInt64(6),
                        MinPrice = reader.GetInt64(7),
                        MaxPrice = reader.GetInt64(8)
                    });
                }
                
                return summaries;
            });
        }

        public async Task<List<Item>> LoadAllItemsAsync(int? guildId = null)
        {
            return await Task.Run(() =>
            {
                var items = new List<Item>();
                
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                using var command = connection.CreateCommand();
                string guildFilter;
                if (guildId.HasValue)
                {
                    guildFilter = "AND s.GuildId = @GuildId";
                }
                else
                {
                    guildFilter = "";
                }
                
                command.CommandText = $@"
                    SELECT 
                        i.ItemId,
                        i.ItemLink,
                        i.Name,
                        COUNT(s.SaleId) as TotalSalesCount,
                        COALESCE(SUM(s.Quantity), 0) as TotalQuantitySold,
                        COALESCE(SUM(s.Price), 0) as TotalValueSold,
                        CASE 
                            WHEN SUM(s.Quantity) > 0 THEN COALESCE(SUM(s.Price) / CAST(SUM(s.Quantity) AS REAL), 0)
                            ELSE 0
                        END as AveragePrice,
                        COALESCE(MIN(s.Price / CAST(s.Quantity AS REAL)), 0) as MinPrice,
                        COALESCE(MAX(s.Price / CAST(s.Quantity AS REAL)), 0) as MaxPrice
                    FROM Items i
                    INNER JOIN ItemSales s ON i.ItemId = s.ItemId
                    WHERE 1=1 {guildFilter}
                    GROUP BY i.ItemId, i.ItemLink, i.Name
                    ORDER BY MAX(s.SaleTimestamp) DESC
                ";
                
                if (guildId.HasValue)
                {
                    command.Parameters.AddWithValue("@GuildId", guildId.Value);
                }
                
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    items.Add(new Item
                    {
                        ItemId = reader.GetInt64(0),
                        ItemLink = reader.GetString(1),
                        Name = reader.GetString(2),
                        TotalSalesCount = reader.GetInt32(3),
                        TotalQuantitySold = reader.GetInt64(4),
                        TotalValueSold = reader.GetInt64(5),
                        AveragePrice = reader.GetInt64(6),
                        MinPrice = reader.GetInt64(7),
                        MaxPrice = reader.GetInt64(8)
                    });
                }
                
                return items;
            });
        }

        public async Task<List<(int GuildId, string GuildName)>> LoadAllGuildsAsync()
        {
            return await Task.Run(() =>
            {
                var guilds = new List<(int, string)>();
                
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT DISTINCT GuildId, GuildName
                    FROM ItemSales
                    ORDER BY GuildName
                ";
                
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    guilds.Add((reader.GetInt32(0), reader.GetString(1)));
                }
                
                return guilds;
            });
        }

        public async Task UpdateItemNameAsync(long itemId, string name)
        {
            await Task.Run(() =>
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE Items
                    SET Name = @Name
                    WHERE ItemId = @ItemId
                ";
                command.Parameters.AddWithValue("@Name", name ?? "");
                command.Parameters.AddWithValue("@ItemId", itemId);
                
                command.ExecuteNonQuery();
            });
        }

        public async Task<Dictionary<long, string>> LoadAllItemNamesAsync()
        {
            return await Task.Run(() =>
            {
                var itemNames = new Dictionary<long, string>();
                
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT ItemId, Name FROM Items";
                
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var itemId = reader.GetInt64(0);
                    var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    itemNames[itemId] = name;
                }
                
                return itemNames;
            });
        }

        public async Task<string> GetItemNameAsync(long itemId)
        {
            return await Task.Run(() =>
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Name
                    FROM Items
                    WHERE ItemId = @ItemId
                ";
                command.Parameters.AddWithValue("@ItemId", itemId);
                
                var result = command.ExecuteScalar();
                return result?.ToString() ?? "";
            });
        }

        public async Task<List<GuildItemSummary>> LoadItemsByGuildAsync()
        {
            return await Task.Run(() =>
            {
                var summaries = new List<GuildItemSummary>();
                
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT 
                        i.ItemId,
                        i.ItemLink,
                        i.Name,
                        s.GuildId,
                        s.GuildName,
                        COALESCE(SUM(s.Price), 0) as TotalValueSold,
                        COUNT(s.SaleId) as TotalSalesCount,
                        COALESCE(SUM(s.Quantity), 0) as TotalQuantitySold
                    FROM Items i
                    INNER JOIN ItemSales s ON i.ItemId = s.ItemId
                    GROUP BY i.ItemId, i.ItemLink, i.Name, s.GuildId, s.GuildName
                    ORDER BY s.GuildName, i.ItemLink
                ";
                
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    summaries.Add(new GuildItemSummary
                    {
                        ItemId = reader.GetInt64(0),
                        ItemLink = reader.GetString(1),
                        Name = reader.GetString(2),
                        GuildId = reader.GetInt32(3),
                        GuildName = reader.GetString(4),
                        TotalValueSold = reader.GetInt64(5),
                        TotalSalesCount = reader.GetInt32(6),
                        TotalQuantitySold = reader.GetInt64(7)
                    });
                }
                
                return summaries;
            });
        }

        public async Task<List<ItemSale>> LoadSalesForItemInGuildAsync(long itemId, int guildId)
        {
            return await Task.Run(() =>
            {
                var sales = new List<ItemSale>();
                
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT 
                        s.SaleId,
                        s.ItemId,
                        i.ItemLink,
                        s.GuildId,
                        s.GuildName,
                        s.Seller,
                        s.Buyer,
                        s.Quantity,
                        s.Price,
                        s.SaleTimestamp,
                        s.DuplicateIndex
                    FROM ItemSales s
                    INNER JOIN Items i ON s.ItemId = i.ItemId
                    WHERE s.ItemId = @ItemId AND s.GuildId = @GuildId
                    ORDER BY s.SaleTimestamp DESC
                ";
                command.Parameters.AddWithValue("@ItemId", itemId);
                command.Parameters.AddWithValue("@GuildId", guildId);
                
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    sales.Add(new ItemSale
                    {
                        SaleId = reader.GetInt64(0),
                        ItemId = reader.GetInt64(1),
                        ItemLink = reader.GetString(2),
                        GuildId = reader.GetInt32(3),
                        GuildName = reader.GetString(4),
                        Seller = reader.GetString(5),
                        Buyer = reader.GetString(6),
                        Quantity = reader.GetInt32(7),
                        Price = reader.GetInt32(8),
                        SaleTimestamp = reader.GetInt64(9),
                        DuplicateIndex = reader.GetInt32(10)
                    });
                }
                
                return sales;
            });
        }

        public async Task<HashSet<string>> GetAllSellersInGuildAsync(int guildId)
        {
            return await Task.Run(() =>
            {
                var sellers = new HashSet<string>();
                
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT DISTINCT Seller
                    FROM ItemSales
                    WHERE GuildId = @GuildId
                ";
                command.Parameters.AddWithValue("@GuildId", guildId);
                
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    sellers.Add(reader.GetString(0));
                }
                
                return sellers;
            });
        }

        public async Task<Dictionary<(long ItemId, int GuildId), int>> CalculateInternalCountsAsync(
            List<GuildItemSummary> guildItems)
        {
            return await Task.Run(() =>
            {
                var internalCounts = new Dictionary<(long ItemId, int GuildId), int>();
                
                if (guildItems.Count == 0)
                    return internalCounts;
                
                // Group items by guild
                var itemsByGuild = guildItems.GroupBy(g => g.GuildId).ToList();
                
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                
                foreach (var guildGroup in itemsByGuild)
                {
                    var guildId = guildGroup.Key;
                    var itemIds = guildGroup.Select(g => g.ItemId).ToList();
                    
                    if (itemIds.Count == 0)
                        continue;
                    
                    // Build parameterized query for all items in this guild at once
                    var itemIdPlaceholders = string.Join(",", itemIds.Select((_, i) => $"@ItemId{i}"));
                    
                    using var countCommand = connection.CreateCommand();
                    countCommand.CommandText = $@"
                        SELECT ItemId, COUNT(DISTINCT SaleId) as InternalCount
                        FROM ItemSales
                        WHERE ItemId IN ({itemIdPlaceholders})
                        AND GuildId = @GuildId
                        AND Buyer IN (
                            SELECT DISTINCT Seller
                            FROM ItemSales
                            WHERE GuildId = @GuildId
                        )
                        GROUP BY ItemId
                    ";
                    
                    for (int i = 0; i < itemIds.Count; i++)
                    {
                        countCommand.Parameters.AddWithValue($"@ItemId{i}", itemIds[i]);
                    }
                    countCommand.Parameters.AddWithValue("@GuildId", guildId);
                    
                    using var reader = countCommand.ExecuteReader();
                    while (reader.Read())
                    {
                        var itemId = reader.GetInt64(0);
                        var count = reader.GetInt32(1);
                        internalCounts[(itemId, guildId)] = count;
                    }
                    
                    // Set 0 for items that had no internal sales
                    foreach (var itemId in itemIds)
                    {
                        if (!internalCounts.ContainsKey((itemId, guildId)))
                        {
                            internalCounts[(itemId, guildId)] = 0;
                        }
                    }
                }
                
                return internalCounts;
            });
        }
    }
}

