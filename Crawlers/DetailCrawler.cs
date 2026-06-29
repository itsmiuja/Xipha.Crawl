using Xipha.Crawl.Models;
using Xipha.Crawl.Parsers;

namespace Xipha.Crawl.Crawlers;

public class DetailCrawler
{
    private readonly HttpClient _http;
    private readonly DetailPageParser _parser = new();
    private readonly CrawlerConfig _config;

    public DetailCrawler(HttpClient http, CrawlerConfig config)
    {
        _http = http;
        _config = config;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(config.UserAgent);
    }

    /// <summary>
    /// Fetches and parses the detail page for a single drug.
    /// Returns null if all MaxRetries attempts fail.
    /// </summary>
    public async Task<DrugDetail?> CrawlAsync(string detailUrl, CancellationToken ct = default)
    {
        for (int attempt = 1; attempt <= _config.MaxRetries; attempt++)
        {
            try
            {
                string html = await FetchAsync(detailUrl, ct);
                return _parser.Parse(html, detailUrl);
            }
            catch (Exception) when (attempt < _config.MaxRetries)
            {
                await Task.Delay(_config.RetryDelayMs * attempt, ct);
            }
            catch (Exception)
            {
                return null;
            }
        }
        return null;
    }

    private async Task<string> FetchAsync(string url, CancellationToken ct)
    {
        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}