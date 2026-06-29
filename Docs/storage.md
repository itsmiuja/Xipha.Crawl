# Storage — Data Layer

## IStorage Interface

All database interaction goes through `IStorage`. This design:
- Improves testability (can be replaced with a mock)
- Makes it possible to swap the backend (e.g. to PostgreSQL) without touching other layers

```csharp
Task InitializeAsync();
Task<List<int>> SaveSearchResultsAsync(IEnumerable<DrugBasic> drugs);
Task<int> SaveDetailAsync(DrugDetail detail);
Task<int> SavePricesAsync(IEnumerable<PriceRecord> records);
Task<IEnumerable<string>> GetDetailUrlsByWebIdsAsync(IEnumerable<int> webIds);
Task<(int Total, int WithDetail)> GetStatsAsync();
```

---

## Database Schema

### Drugs table

One row per drug — basic information only.

```sql
CREATE TABLE Drugs (
    Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    WebId              INTEGER UNIQUE,        -- numeric ID from site URL
    PersianName        TEXT,
    EnglishName        TEXT,
    BrandOwner         TEXT,
    LicenseHolder      TEXT,
    Packaging          TEXT,
    ProductCode        TEXT,
    GenericCode        TEXT,
    DetailUrl          TEXT UNIQUE,
    SearchTermUsed     TEXT,                  -- first prefix that found this drug
    IsEmergencyLicense INTEGER DEFAULT 0,
    IsDetailScraped    INTEGER DEFAULT 0,     -- flag: has detail page been crawled?
    ScrapedAt          DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

### DrugDetails table

One row per drug — full information (populated only after detail crawl).

```sql
CREATE TABLE DrugDetails (
    WebId         INTEGER PRIMARY KEY,
    DetailUrl     TEXT,
    BrandName     TEXT,
    GenericName   TEXT,
    DrugForm      TEXT,
    RouteOfAdmin  TEXT,
    LicenseHolder TEXT,
    BrandOwner    TEXT,
    Manufacturer  TEXT,
    LicenseExpiry TEXT,
    GTIN          TEXT,
    IRC           TEXT,
    Packaging     TEXT,
    Composition   TEXT,
    ATCCode       TEXT,
    ATCHierarchy  TEXT,    -- JSON: [{"Code":"C","Description":"..."},...]
    ScrapedAt     DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (WebId) REFERENCES Drugs(WebId)
);
```

### PriceHistory table

One row per price change — many rows per drug over time.

```sql
CREATE TABLE PriceHistory (
    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
    WebId        INTEGER NOT NULL,
    PackagePrice INTEGER,
    UnitPrice    INTEGER,
    UnitCount    INTEGER,
    Source       TEXT,         -- "search" | "detail"
    RecordedAt   DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (WebId) REFERENCES Drugs(WebId)
);

CREATE INDEX idx_price_webid ON PriceHistory(WebId, RecordedAt DESC);
```

The index on `(WebId, RecordedAt DESC)` supports fast "latest price for a drug" queries.

---

## Key Logic

### 1. INSERT OR IGNORE in SaveSearchResults

```sql
INSERT OR IGNORE INTO Drugs (WebId, ...) VALUES (@WebId, ...);
```

If a drug with the same `WebId` already exists, the entire INSERT is silently skipped. The method returns only the WebIds of rows that were actually created (`rows > 0`).

### 2. Price recorded only on change

```sql
INSERT INTO PriceHistory (WebId, PackagePrice, ...)
SELECT @WebId, @PackagePrice, ...
WHERE EXISTS (SELECT 1 FROM Drugs WHERE WebId = @WebId)
  AND COALESCE(
        (SELECT PackagePrice FROM PriceHistory
         WHERE WebId = @WebId
         ORDER BY RecordedAt DESC LIMIT 1),
        -1
      ) != @PackagePrice;
```

This single query:
1. Verifies the drug exists in `Drugs`
2. Reads the last recorded price
3. Inserts a new row only if the price has changed (or no price exists yet)

`COALESCE(..., -1)` ensures the very first price record is always written.

### 3. INSERT OR REPLACE in SaveDetail

```sql
INSERT OR REPLACE INTO DrugDetails (WebId, ...) VALUES (@WebId, ...);
```

If a detail row for the same `WebId` already exists, it is replaced (not ignored). This supports refreshing clinical data in future runs.

### 4. Chunking in GetDetailUrls

```csharp
foreach (var chunk in ids.Chunk(500))
```

SQLite has a parameter limit of approximately 999. WebIds are split into chunks of 500 to stay safely within this limit.

---

## Transactions

Every write operation runs inside a transaction:

| Method | Transaction scope |
|---|---|
| `SaveSearchResultsAsync` | One transaction for all drugs in the call |
| `SaveDetailAsync` | One transaction: INSERT detail + UPDATE flag |
| `SavePricesAsync` | One transaction for all PriceRecords in the call |

---

## Connection Management

```csharp
private SqliteConnection Open() => new($"Data Source={_dbPath}");
```

Each method creates a new connection and disposes it with `await using`. This simple approach is sufficient for the serial workload of this project.

---

## Useful Queries

```sql
-- Latest price for every drug
SELECT d.PersianName, d.EnglishName, p.PackagePrice, p.UnitPrice, p.RecordedAt
FROM Drugs d
JOIN PriceHistory p ON p.WebId = d.WebId
WHERE p.Id = (
    SELECT Id FROM PriceHistory
    WHERE WebId = d.WebId
    ORDER BY RecordedAt DESC LIMIT 1
);

-- Price history for a specific drug
SELECT PackagePrice, UnitPrice, Source, RecordedAt
FROM PriceHistory
WHERE WebId = 12345
ORDER BY RecordedAt;

-- Drugs that have not been detail-crawled yet
SELECT PersianName, DetailUrl
FROM Drugs
WHERE IsDetailScraped = 0;
```
