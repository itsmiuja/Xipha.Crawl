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

    // ── Manual CRUD (used by the REST API) ──────────────────────

    public async Task<(List<DrugRecord> Items, int TotalCount)> GetDrugsAsync(int skip, int take, string? search)
    {
        await using var conn = Open();
        await conn.OpenAsync();

        string where = string.IsNullOrWhiteSpace(search)
            ? ""
            : "WHERE PersianName LIKE @s OR EnglishName LIKE @s OR ProductCode LIKE @s OR GenericCode LIKE @s";

        await using var countCmd = new SqliteCommand($"SELECT COUNT(*) FROM Drugs {where};", conn);
        if (where != "") countCmd.Parameters.AddWithValue("@s", $"%{search}%");
        int total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

        var sql = $@"
            SELECT Id, WebId, PersianName, EnglishName, BrandOwner, LicenseHolder,
                   Packaging, ProductCode, GenericCode, DetailUrl, SearchTermUsed,
                   IsEmergencyLicense, IsDetailScraped, ScrapedAt
            FROM Drugs {where}
            ORDER BY Id DESC
            LIMIT @take OFFSET @skip;";

        await using var cmd = new SqliteCommand(sql, conn);
        if (where != "") cmd.Parameters.AddWithValue("@s", $"%{search}%");
        cmd.Parameters.AddWithValue("@take", take);
        cmd.Parameters.AddWithValue("@skip", skip);

        var items = new List<DrugRecord>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) items.Add(ReadDrugRecord(r));

        return (items, total);
    }

    public async Task<DrugRecord?> GetDrugByIdAsync(int id)
    {
        await using var conn = Open();
        await conn.OpenAsync();

        const string sql = @"
            SELECT Id, WebId, PersianName, EnglishName, BrandOwner, LicenseHolder,
                   Packaging, ProductCode, GenericCode, DetailUrl, SearchTermUsed,
                   IsEmergencyLicense, IsDetailScraped, ScrapedAt
            FROM Drugs WHERE Id = @Id;";

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? ReadDrugRecord(r) : null;
    }

    public async Task<int?> CreateDrugAsync(DrugBasic d)
    {
        await using var conn = Open();
        await conn.OpenAsync();

        const string sql = @"
            INSERT INTO Drugs
              (WebId, PersianName, EnglishName, BrandOwner, LicenseHolder,
               Packaging, ProductCode, GenericCode, DetailUrl,
               SearchTermUsed, IsEmergencyLicense)
            VALUES
              (@WebId, @PersianName, @EnglishName, @BrandOwner, @LicenseHolder,
               @Packaging, @ProductCode, @GenericCode, @DetailUrl,
               @SearchTermUsed, @IsEmergencyLicense);
            SELECT last_insert_rowid();";

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@WebId", d.WebId);
        cmd.Parameters.AddWithValue("@PersianName", d.PersianName);
        cmd.Parameters.AddWithValue("@EnglishName", d.EnglishName);
        cmd.Parameters.AddWithValue("@BrandOwner", d.BrandOwner);
        cmd.Parameters.AddWithValue("@LicenseHolder", d.LicenseHolder);
        cmd.Parameters.AddWithValue("@Packaging", d.Packaging);
        cmd.Parameters.AddWithValue("@ProductCode", d.ProductCode);
        cmd.Parameters.AddWithValue("@GenericCode", d.GenericCode);
        cmd.Parameters.AddWithValue("@DetailUrl", d.DetailUrl);
        cmd.Parameters.AddWithValue("@SearchTermUsed", string.IsNullOrWhiteSpace(d.SearchTermUsed) ? "manual" : d.SearchTermUsed);
        cmd.Parameters.AddWithValue("@IsEmergencyLicense", d.IsEmergencyLicense ? 1 : 0);

        try
        {
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // UNIQUE constraint failed
        {
            return null;
        }
    }

    public async Task<bool> UpdateDrugAsync(int id, DrugBasic d)
    {
        await using var conn = Open();
        await conn.OpenAsync();

        const string sql = @"
            UPDATE Drugs SET
                WebId = @WebId, PersianName = @PersianName, EnglishName = @EnglishName,
                BrandOwner = @BrandOwner, LicenseHolder = @LicenseHolder, Packaging = @Packaging,
                ProductCode = @ProductCode, GenericCode = @GenericCode, DetailUrl = @DetailUrl,
                IsEmergencyLicense = @IsEmergencyLicense
            WHERE Id = @Id;";

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@WebId", d.WebId);
        cmd.Parameters.AddWithValue("@PersianName", d.PersianName);
        cmd.Parameters.AddWithValue("@EnglishName", d.EnglishName);
        cmd.Parameters.AddWithValue("@BrandOwner", d.BrandOwner);
        cmd.Parameters.AddWithValue("@LicenseHolder", d.LicenseHolder);
        cmd.Parameters.AddWithValue("@Packaging", d.Packaging);
        cmd.Parameters.AddWithValue("@ProductCode", d.ProductCode);
        cmd.Parameters.AddWithValue("@GenericCode", d.GenericCode);
        cmd.Parameters.AddWithValue("@DetailUrl", d.DetailUrl);
        cmd.Parameters.AddWithValue("@IsEmergencyLicense", d.IsEmergencyLicense ? 1 : 0);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteDrugAsync(int id)
    {
        await using var conn = Open();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        // DrugDetails/PriceHistory reference WebId, not Id — look it up first
        int webId;
        await using (var lookup = new SqliteCommand("SELECT WebId FROM Drugs WHERE Id = @Id", conn, (SqliteTransaction)tx))
        {
            lookup.Parameters.AddWithValue("@Id", id);
            var result = await lookup.ExecuteScalarAsync();
            if (result is null) { await tx.RollbackAsync(); return false; }
            webId = Convert.ToInt32(result);
        }

        await using (var delPrices = new SqliteCommand("DELETE FROM PriceHistory WHERE WebId = @WebId", conn, (SqliteTransaction)tx))
        {
            delPrices.Parameters.AddWithValue("@WebId", webId);
            await delPrices.ExecuteNonQueryAsync();
        }

        await using (var delDetail = new SqliteCommand("DELETE FROM DrugDetails WHERE WebId = @WebId", conn, (SqliteTransaction)tx))
        {
            delDetail.Parameters.AddWithValue("@WebId", webId);
            await delDetail.ExecuteNonQueryAsync();
        }


        await using var del = new SqliteCommand("DELETE FROM Drugs WHERE Id = @Id", conn, (SqliteTransaction)tx);
        del.Parameters.AddWithValue("@Id", id);
        int rows = await del.ExecuteNonQueryAsync();

        await tx.CommitAsync();
        return rows > 0;
    }

    public async Task<DrugDetail?> GetDrugDetailByWebIdAsync(int webId)
    {
        await using var conn = Open();
        await conn.OpenAsync();

        const string sql = @"
            SELECT WebId, DetailUrl, BrandName, GenericName, DrugForm, RouteOfAdmin,
                   LicenseHolder, BrandOwner, Manufacturer, LicenseExpiry,
                   GTIN, IRC, Packaging, Composition, ATCCode, ATCHierarchy, ScrapedAt
            FROM DrugDetails WHERE WebId = @WebId;";

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@WebId", webId);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new DrugDetail
        {
            WebId = r.GetInt32(0),
            DetailUrl = r.GetString(1),
            BrandName = r.GetString(2),
            GenericName = r.GetString(3),
            DrugForm = r.GetString(4),
            RouteOfAdmin = r.GetString(5),
            LicenseHolder = r.GetString(6),
            BrandOwner = r.GetString(7),
            Manufacturer = r.GetString(8),
            LicenseExpiry = r.GetString(9),
            GTIN = r.GetString(10),
            IRC = r.GetString(11),
            Packaging = r.GetString(12),
            Composition = r.GetString(13),
            ATCCode = r.GetString(14),
            ATCHierarchy = JsonSerializer.Deserialize<List<ATCLevel>>(r.GetString(15)) ?? [],
            ScrapedAt = DateTime.Parse(r.GetString(16))
        };
    }

    public async Task<bool> DeleteDrugDetailAsync(int webId)
    {
        await using var conn = Open();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await using var del = new SqliteCommand("DELETE FROM DrugDetails WHERE WebId = @WebId", conn, (SqliteTransaction)tx);
        del.Parameters.AddWithValue("@WebId", webId);
        int rows = await del.ExecuteNonQueryAsync();

        await using var upd = new SqliteCommand("UPDATE Drugs SET IsDetailScraped = 0 WHERE WebId = @WebId", conn, (SqliteTransaction)tx);
        upd.Parameters.AddWithValue("@WebId", webId);
        await upd.ExecuteNonQueryAsync();

        await tx.CommitAsync();
        return rows > 0;
    }

    public async Task<(List<PriceRecord> Items, int TotalCount)> GetPriceHistoryAsync(int webId, int skip, int take)
    {
        await using var conn = Open();
        await conn.OpenAsync();

        await using var countCmd = new SqliteCommand("SELECT COUNT(*) FROM PriceHistory WHERE WebId = @WebId;", conn);
        countCmd.Parameters.AddWithValue("@WebId", webId);
        int total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

        const string sql = @"
            SELECT Id, WebId, PackagePrice, UnitPrice, UnitCount, Source, RecordedAt
            FROM PriceHistory WHERE WebId = @WebId
            ORDER BY RecordedAt DESC
            LIMIT @take OFFSET @skip;";

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@WebId", webId);
        cmd.Parameters.AddWithValue("@take", take);
        cmd.Parameters.AddWithValue("@skip", skip);

        var items = new List<PriceRecord>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) items.Add(ReadPriceRecord(r));

        return (items, total);
    }

    public async Task<PriceRecord?> GetPriceRecordByIdAsync(int id)
    {
        await using var conn = Open();
        await conn.OpenAsync();

        const string sql = @"
            SELECT Id, WebId, PackagePrice, UnitPrice, UnitCount, Source, RecordedAt
            FROM PriceHistory WHERE Id = @Id;";

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? ReadPriceRecord(r) : null;
    }

    public async Task<int?> CreatePriceRecordManualAsync(PriceRecord rec)
    {
        await using var conn = Open();
        await conn.OpenAsync();

        await using (var check = new SqliteCommand("SELECT 1 FROM Drugs WHERE WebId = @WebId", conn))
        {
            check.Parameters.AddWithValue("@WebId", rec.WebId);
            if (await check.ExecuteScalarAsync() is null) return null;
        }

        const string sql = @"
            INSERT INTO PriceHistory (WebId, PackagePrice, UnitPrice, UnitCount, Source, RecordedAt)
            VALUES (@WebId, @PackagePrice, @UnitPrice, @UnitCount, @Source, @RecordedAt);
            SELECT last_insert_rowid();";

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@WebId", rec.WebId);
        cmd.Parameters.AddWithValue("@PackagePrice", rec.PackagePrice);
        cmd.Parameters.AddWithValue("@UnitPrice", rec.UnitPrice);
        cmd.Parameters.AddWithValue("@UnitCount", rec.UnitCount);
        cmd.Parameters.AddWithValue("@Source", string.IsNullOrWhiteSpace(rec.Source) ? "manual" : rec.Source);
        cmd.Parameters.AddWithValue("@RecordedAt", (rec.RecordedAt == default ? DateTime.UtcNow : rec.RecordedAt).ToString("o"));

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<bool> UpdatePriceRecordAsync(int id, PriceRecord rec)
    {
        await using var conn = Open();
        await conn.OpenAsync();

        const string sql = @"
            UPDATE PriceHistory SET
                PackagePrice = @PackagePrice, UnitPrice = @UnitPrice,
                UnitCount = @UnitCount, Source = @Source
            WHERE Id = @Id;";

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@PackagePrice", rec.PackagePrice);
        cmd.Parameters.AddWithValue("@UnitPrice", rec.UnitPrice);
        cmd.Parameters.AddWithValue("@UnitCount", rec.UnitCount);
        cmd.Parameters.AddWithValue("@Source", string.IsNullOrWhiteSpace(rec.Source) ? "manual" : rec.Source);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeletePriceRecordAsync(int id)
    {
        await using var conn = Open();
        await conn.OpenAsync();

        await using var cmd = new SqliteCommand("DELETE FROM PriceHistory WHERE Id = @Id;", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ── Reader helpers ────────────────────────────────────────

    private static DrugRecord ReadDrugRecord(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        WebId = r.GetInt32(1),
        PersianName = r.GetString(2),
        EnglishName = r.GetString(3),
        BrandOwner = r.GetString(4),
        LicenseHolder = r.GetString(5),
        Packaging = r.GetString(6),
        ProductCode = r.GetString(7),
        GenericCode = r.GetString(8),
        DetailUrl = r.GetString(9),
        SearchTermUsed = r.GetString(10),
        IsEmergencyLicense = r.GetInt32(11) == 1,
        IsDetailScraped = r.GetInt32(12) == 1,
        ScrapedAt = DateTime.Parse(r.GetString(13))
    };

    private static PriceRecord ReadPriceRecord(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        WebId = r.GetInt32(1),
        PackagePrice = r.GetInt64(2),
        UnitPrice = r.GetInt64(3),
        UnitCount = r.GetInt32(4),
        Source = r.GetString(5),
        RecordedAt = DateTime.Parse(r.GetString(6))
    };

    // ── Internals ─────────────────────────────────────────────

    private SqliteConnection Open() => new($"Data Source={_dbPath}");

    private static async Task Exec(SqliteConnection conn, string sql)
    {
        await using var cmd = new SqliteCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private string Serialize<T>(T obj) => JsonSerializer.Serialize(obj, _json);
}