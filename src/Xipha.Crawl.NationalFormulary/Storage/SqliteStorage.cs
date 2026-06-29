using System.Text.Json;
using Microsoft.Data.Sqlite;
using Xipha.Crawl.NationalFormulary.Models;

namespace Xipha.Crawl.NationalFormulary.Storage;

public class SqliteStorage : IStorage
{
    private readonly string _dbPath;
    private readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    public SqliteStorage(string dbPath) => _dbPath = dbPath;

    // ── Init ──────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        await using var conn = Open();
        await conn.OpenAsync();

        // Basic drug information (no prices)
        await Exec(conn, @"
            CREATE TABLE IF NOT EXISTS Drugs (
                Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
                WebId              INTEGER UNIQUE,
                PersianName        TEXT,
                EnglishName        TEXT,
                BrandOwner         TEXT,
                LicenseHolder      TEXT,
                Packaging          TEXT,
                ProductCode        TEXT,
                GenericCode        TEXT,
                DetailUrl          TEXT UNIQUE,
                SearchTermUsed     TEXT,
                IsEmergencyLicense INTEGER DEFAULT 0,
                IsDetailScraped    INTEGER DEFAULT 0,
                ScrapedAt          DATETIME DEFAULT CURRENT_TIMESTAMP
            );");

        // Full detail record (no clinical fields, no prices)
        await Exec(conn, @"
            CREATE TABLE IF NOT EXISTS DrugDetails (
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
                ATCHierarchy  TEXT,
                ScrapedAt     DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (WebId) REFERENCES Drugs(WebId)
            );");

        // Price history — a new row is inserted on every price change
        await Exec(conn, @"
            CREATE TABLE IF NOT EXISTS PriceHistory (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                WebId        INTEGER NOT NULL,
                PackagePrice INTEGER,
                UnitPrice    INTEGER,
                UnitCount    INTEGER,
                Source       TEXT,
                RecordedAt   DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (WebId) REFERENCES Drugs(WebId)
            );");

        await Exec(conn, "CREATE INDEX IF NOT EXISTS idx_price_webid ON PriceHistory(WebId, RecordedAt DESC);");
    }

    // ── Search Results ────────────────────────────────────────

    public async Task<List<int>> SaveSearchResultsAsync(IEnumerable<DrugBasic> drugs)
    {
        await using var conn = Open();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var newWebIds = new List<int>();

        const string sql = @"
            INSERT OR IGNORE INTO Drugs
              (WebId, PersianName, EnglishName, BrandOwner, LicenseHolder,
               Packaging, ProductCode, GenericCode, DetailUrl,
               SearchTermUsed, IsEmergencyLicense)
            VALUES
              (@WebId, @PersianName, @EnglishName, @BrandOwner, @LicenseHolder,
               @Packaging, @ProductCode, @GenericCode, @DetailUrl,
               @SearchTermUsed, @IsEmergencyLicense);";

        foreach (var d in drugs)
        {
            await using var cmd = new SqliteCommand(sql, conn, (SqliteTransaction)tx);
            cmd.Parameters.AddWithValue("@WebId", d.WebId);
            cmd.Parameters.AddWithValue("@PersianName", d.PersianName);
            cmd.Parameters.AddWithValue("@EnglishName", d.EnglishName);
            cmd.Parameters.AddWithValue("@BrandOwner", d.BrandOwner);
            cmd.Parameters.AddWithValue("@LicenseHolder", d.LicenseHolder);
            cmd.Parameters.AddWithValue("@Packaging", d.Packaging);
            cmd.Parameters.AddWithValue("@ProductCode", d.ProductCode);
            cmd.Parameters.AddWithValue("@GenericCode", d.GenericCode);
            cmd.Parameters.AddWithValue("@DetailUrl", d.DetailUrl);
            cmd.Parameters.AddWithValue("@SearchTermUsed", d.SearchTermUsed);
            cmd.Parameters.AddWithValue("@IsEmergencyLicense", d.IsEmergencyLicense ? 1 : 0);

            int rows = await cmd.ExecuteNonQueryAsync();
            if (rows > 0 && d.WebId > 0) newWebIds.Add(d.WebId);
        }

        await tx.CommitAsync();
        return newWebIds;
    }

    // ── Detail ────────────────────────────────────────────────

    public async Task<int> SaveDetailAsync(DrugDetail d)
    {
        await using var conn = Open();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        const string sql = @"
            INSERT OR REPLACE INTO DrugDetails
              (WebId, DetailUrl, BrandName, GenericName, DrugForm, RouteOfAdmin,
               LicenseHolder, BrandOwner, Manufacturer, LicenseExpiry,
               GTIN, IRC, Packaging, Composition, ATCCode, ATCHierarchy, ScrapedAt)
            VALUES
              (@WebId, @DetailUrl, @BrandName, @GenericName, @DrugForm, @RouteOfAdmin,
               @LicenseHolder, @BrandOwner, @Manufacturer, @LicenseExpiry,
               @GTIN, @IRC, @Packaging, @Composition, @ATCCode, @ATCHierarchy, @ScrapedAt);";

        await using var cmd = new SqliteCommand(sql, conn, (SqliteTransaction)tx);
        cmd.Parameters.AddWithValue("@WebId", d.WebId);
        cmd.Parameters.AddWithValue("@DetailUrl", d.DetailUrl);
        cmd.Parameters.AddWithValue("@BrandName", d.BrandName);
        cmd.Parameters.AddWithValue("@GenericName", d.GenericName);
        cmd.Parameters.AddWithValue("@DrugForm", d.DrugForm);
        cmd.Parameters.AddWithValue("@RouteOfAdmin", d.RouteOfAdmin);
        cmd.Parameters.AddWithValue("@LicenseHolder", d.LicenseHolder);
        cmd.Parameters.AddWithValue("@BrandOwner", d.BrandOwner);
        cmd.Parameters.AddWithValue("@Manufacturer", d.Manufacturer);
        cmd.Parameters.AddWithValue("@LicenseExpiry", d.LicenseExpiry);
        cmd.Parameters.AddWithValue("@GTIN", d.GTIN);
        cmd.Parameters.AddWithValue("@IRC", d.IRC);
        cmd.Parameters.AddWithValue("@Packaging", d.Packaging);
        cmd.Parameters.AddWithValue("@Composition", d.Composition);
        cmd.Parameters.AddWithValue("@ATCCode", d.ATCCode);
        cmd.Parameters.AddWithValue("@ATCHierarchy", Serialize(d.ATCHierarchy));
        cmd.Parameters.AddWithValue("@ScrapedAt", d.ScrapedAt.ToString("o"));
        int rows = await cmd.ExecuteNonQueryAsync();

        await using var upd = new SqliteCommand(
            "UPDATE Drugs SET IsDetailScraped = 1 WHERE WebId = @WebId",
            conn, (SqliteTransaction)tx);
        upd.Parameters.AddWithValue("@WebId", d.WebId);
        await upd.ExecuteNonQueryAsync();

        await tx.CommitAsync();
        return rows;
    }

    // ── Prices ────────────────────────────────────────────────

    public async Task<int> SavePricesAsync(IEnumerable<PriceRecord> records)
    {
        await using var conn = Open();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        int count = 0;

        // Insert only when:
        //   1. The drug already exists in the Drugs table
        //   2. The price differs from the most recent entry (or no entry exists yet)
        const string sql = @"
            INSERT INTO PriceHistory (WebId, PackagePrice, UnitPrice, UnitCount, Source)
            SELECT @WebId, @PackagePrice, @UnitPrice, @UnitCount, @Source
            WHERE EXISTS (SELECT 1 FROM Drugs WHERE WebId = @WebId)
              AND COALESCE(
                    (SELECT PackagePrice FROM PriceHistory
                     WHERE WebId = @WebId
                     ORDER BY RecordedAt DESC LIMIT 1),
                    -1
                  ) != @PackagePrice;";

        foreach (var r in records.Where(r => r.WebId > 0 && r.PackagePrice > 0))
        {
            await using var cmd = new SqliteCommand(sql, conn, (SqliteTransaction)tx);
            cmd.Parameters.AddWithValue("@WebId", r.WebId);
            cmd.Parameters.AddWithValue("@PackagePrice", r.PackagePrice);
            cmd.Parameters.AddWithValue("@UnitPrice", r.UnitPrice);
            cmd.Parameters.AddWithValue("@UnitCount", r.UnitCount);
            cmd.Parameters.AddWithValue("@Source", r.Source);
            count += await cmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
        return count;
    }

    // ── Queries ───────────────────────────────────────────────

    public async Task<IEnumerable<string>> GetDetailUrlsByWebIdsAsync(IEnumerable<int> webIds)
    {
        var ids = webIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        var allUrls = new List<string>();

        // SQLite parameter limit is ~999 — process in chunks of 500
        foreach (var chunk in ids.Chunk(500))
        {
            await using var conn = Open();
            await conn.OpenAsync();

            var parms = chunk.Select((_, i) => $"@id{i}");
            var sql = $@"SELECT DetailUrl FROM Drugs
                          WHERE WebId IN ({string.Join(",", parms)})
                            AND IsDetailScraped = 0
                            AND DetailUrl != ''
                          ORDER BY Id;";

            await using var cmd = new SqliteCommand(sql, conn);
            for (int i = 0; i < chunk.Length; i++)
                cmd.Parameters.AddWithValue($"@id{i}", chunk[i]);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                allUrls.Add(reader.GetString(0));
        }

        return allUrls;
    }

    public async Task<(int Total, int WithDetail)> GetStatsAsync()
    {
        await using var conn = Open();
        await conn.OpenAsync();

        await using var cmd = new SqliteCommand(
            "SELECT COUNT(*), COALESCE(SUM(IsDetailScraped), 0) FROM Drugs",
            conn);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return (0, 0);
        return (r.GetInt32(0), r.GetInt32(1));
    }

    // ── Internals ─────────────────────────────────────────────

    private SqliteConnection Open() => new($"Data Source={_dbPath}");

    private static async Task Exec(SqliteConnection conn, string sql)
    {
        await using var cmd = new SqliteCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private string Serialize<T>(T obj) => JsonSerializer.Serialize(obj, _json);
}