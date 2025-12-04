-- MarketLensESO Database Schema
-- SQLite Database for Item Sales Tracking

-- Enable foreign key constraints
PRAGMA foreign_keys = ON;

-- ==============================================
-- ITEMS AND SALES TRACKING
-- ==============================================

-- Items table (unique by link)
CREATE TABLE Items (
    ItemId INTEGER PRIMARY KEY AUTOINCREMENT,
    ItemLink TEXT NOT NULL UNIQUE,
    FirstSeenDate INTEGER NOT NULL,
    LastSeenDate INTEGER NOT NULL
);

-- ItemSales table (tracks all sales of items)
CREATE TABLE ItemSales (
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
);

-- ==============================================
-- INDEXES FOR PERFORMANCE
-- ==============================================

-- Items indexes
CREATE INDEX IX_Items_ItemLink ON Items(ItemLink);
CREATE INDEX IX_Items_LastSeenDate ON Items(LastSeenDate);

-- ItemSales indexes
CREATE INDEX IX_ItemSales_ItemId ON ItemSales(ItemId);
CREATE INDEX IX_ItemSales_GuildId ON ItemSales(GuildId);
CREATE INDEX IX_ItemSales_Seller ON ItemSales(Seller);
CREATE INDEX IX_ItemSales_Buyer ON ItemSales(Buyer);
CREATE INDEX IX_ItemSales_SaleTimestamp ON ItemSales(SaleTimestamp);
CREATE INDEX IX_ItemSales_ContentHash ON ItemSales(ContentHash);
CREATE INDEX IX_ItemSales_DuplicateIndex ON ItemSales(DuplicateIndex);

