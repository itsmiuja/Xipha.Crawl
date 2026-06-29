using Xipha.Crawl.Models;

namespace Xipha.Crawl.Storage;

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
}