using Xipha.Crawl.NationalFormulary.Models;
using Xipha.Crawl.NationalFormulary.Parsers;

namespace Xipha.Crawl.NationalFormulary.Crawlers;

public class SearchCrawler
{
    private readonly HttpClient _http;
    private readonly SearchResultParser _parser = new();
    private readonly CrawlerConfig _config;

    public SearchCrawler(HttpClient http, CrawlerConfig config)
    {
        _http = http;
        _config = config;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(config.UserAgent);
    }

    /// <summary>
    /// Searches for a single keyword and returns the list of matching drugs.
    /// Retries up to MaxRetries times on failure.
    /// </summary>
    public async Task<List<DrugBasic>> CrawlAsync(string term, CancellationToken ct = default)
    {
        for (int attempt = 1; attempt <= _config.MaxRetries; attempt++)
        {
            try
            {
                string html = await FetchAsync(term, ct);
                return _parser.Parse(html, term);
            }
            catch (Exception) when (attempt < _config.MaxRetries)
            {
                await Task.Delay(_config.RetryDelayMs * attempt, ct);
            }
        }
        return []; // all retries exhausted
    }

    private async Task<string> FetchAsync(string term, CancellationToken ct)
    {
        var url = $"{_config.BaseUrl}/NFI/Search" +
                  $"?Term={Uri.EscapeDataString(term)}" +
                  $"&PageNumber=1&PageSize={_config.PageSize}";

        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}