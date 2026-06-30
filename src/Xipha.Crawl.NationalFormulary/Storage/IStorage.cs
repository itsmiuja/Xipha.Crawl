using Xipha.Crawl.NationalFormulary.Models;

namespace Xipha.Crawl.NationalFormulary.Storage;

public interface IStorage
{
    /// <summary>Creates tables if they do not already exist</summary>
    Task InitializeAsync();

    /// <summary>
    /// Saves search results — duplicate records are silently ignored.
    /// Returns the WebIds of records newly inserted during this call.
    /// </summary>
    Task<List<int>> SaveSearchResultsAsync(IEnumerable<DrugBasic> drugs);

    /// <summary>Saves full detail for a single drug and sets IsDetailScraped = 1</summary>
    Task<int> SaveDetailAsync(DrugDetail detail);

    /// <summary>
    /// Saves a price record — only if the price has changed since the last entry.
    /// Only applies to drugs already present in the Drugs table.
    /// </summary>
    Task<int> SavePricesAsync(IEnumerable<PriceRecord> records);

    /// <summary>Returns DetailUrls for the given WebIds that still have IsDetailScraped = 0</summary>
    Task<IEnumerable<string>> GetDetailUrlsByWebIdsAsync(IEnumerable<int> webIds);

    /// <summary>Overall stats: (total records, records that have detail data)</summary>
    Task<(int Total, int WithDetail)> GetStatsAsync();

    // ── Manual CRUD (used by the REST API) ──────────────────────

    /// <summary>Paged list of products, optionally filtered by name/code search term</summary>
    Task<(List<DrugRecord> Items, int TotalCount)> GetDrugsAsync(int skip, int take, string? search);

    Task<DrugRecord?> GetDrugByIdAsync(int id);

    /// <summary>Manually inserts a product. Returns null if WebId/DetailUrl already exists.</summary>
    Task<int?> CreateDrugAsync(DrugBasic drug);

    /// <summary>Manually updates a product by Id. Returns false if no row matched.</summary>
    Task<bool> UpdateDrugAsync(int id, DrugBasic drug);

    /// <summary>Deletes a product and cascades to its DrugDetails + PriceHistory rows.</summary>
    Task<bool> DeleteDrugAsync(int id);

    Task<DrugDetail?> GetDrugDetailByWebIdAsync(int webId);

    /// <summary>Deletes the detail row and resets IsDetailScraped so it can be re-crawled.</summary>
    Task<bool> DeleteDrugDetailAsync(int webId);

    /// <summary>Paged price history for a product, most recent first.</summary>
    Task<(List<PriceRecord> Items, int TotalCount)> GetPriceHistoryAsync(int webId, int skip, int take);

    Task<PriceRecord?> GetPriceRecordByIdAsync(int id);

    /// <summary>Manually inserts a price record, bypassing the change-detection check. Null if WebId doesn't exist.</summary>
    Task<int?> CreatePriceRecordManualAsync(PriceRecord record);

    Task<bool> UpdatePriceRecordAsync(int id, PriceRecord record);

    Task<bool> DeletePriceRecordAsync(int id);
}