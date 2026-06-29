using System.Collections.Generic;
using Xipha.Crawl.Crawlers;
using Xipha.Crawl.Models;
using Xipha.Crawl.Parsers;
using Xipha.Crawl.Storage;

namespace Xipha.Crawl.Pipeline;

public class CrawlPipeline
{
    private readonly SearchCrawler _searchCrawler;
    private readonly DetailCrawler _detailCrawler;
    private readonly IStorage _storage;
    private readonly CrawlerConfig _config;

    // WebId های جدیدی که در اجرای جاری Phase 1 پیدا شدند
    private readonly List<int> _sessionNewWebIds = [];

    public event Action<string>? OnLog;
    public event Action<CrawlProgress>? OnProgress;

    public CrawlPipeline(
        SearchCrawler searchCrawler,
        DetailCrawler detailCrawler,
        IStorage storage,
        CrawlerConfig config)
    {
        _searchCrawler = searchCrawler;
        _detailCrawler = detailCrawler;
        _storage = storage;
        _config = config;
    }

    // ── Phase 1: Search ───────────────────────────────────────

    /// <summary>
    /// سرچ با کلیدواژه‌ها — فقط داروهای جدید اضافه می‌شوند.
    /// قیمت همه داروهای یافت‌شده (جدید و قدیم) ثبت می‌شود اگر تغییر کرده باشد.
    /// </summary>
    public async Task RunSearchPhaseAsync(
        IEnumerable<string> terms,
        CancellationToken ct = default)
    {
        await _storage.InitializeAsync();
        _sessionNewWebIds.Clear();

        var list = terms.ToList();
        int i = 0;

        Log($"🔍 Phase 1 — Search — {list.Count} terms");

        foreach (var term in list)
        {
            ct.ThrowIfCancellationRequested();
            i++;

            try
            {
                var drugs = await _searchCrawler.CrawlAsync(term, ct);
                var newIds = await _storage.SaveSearchResultsAsync(drugs);
                _sessionNewWebIds.AddRange(newIds);

                // ثبت قیمت برای همه داروهای یافت‌شده (اگر تغییر کرده)
                int prices = await _storage.SavePricesAsync(ToPriceRecords(drugs, "search"));

                Log($"[{i}/{list.Count}] {term,-10} → found:{drugs.Count,-5} new:{newIds.Count,-4} Δprice:{prices}");
            }
            catch (Exception ex)
            {
                Log($"[{i}/{list.Count}] {term,-10} → ERROR: {ex.Message}");
            }

            OnProgress?.Invoke(new CrawlProgress(CrawlPhase.Search, i, list.Count, _sessionNewWebIds.Count));
            if (i < list.Count) await Task.Delay(_config.SearchDelayMs, ct);
        }

        Log($"✅ Phase 1 done — {_sessionNewWebIds.Count} new drugs added");
    }

    // ── Phase 2: Detail ───────────────────────────────────────

    /// <summary>
    /// جزئیات داروها — فقط برای داروهایی که در همین اجرا (Phase 1) جدید بودند.
    /// داروهایی که قبلاً Detail گرفته شده‌اند کراول نمی‌شوند.
    /// </summary>
    public async Task RunDetailPhaseAsync(CancellationToken ct = default)
    {
        var urls = (await _storage.GetDetailUrlsByWebIdsAsync(_sessionNewWebIds)).ToList();

        Log($"📄 Phase 2 — Detail — {urls.Count} new drugs to crawl");

        if (urls.Count == 0)
        {
            Log("ℹ️  Nothing new to detail-crawl.");
            return;
        }

        int done = 0, failed = 0;

        foreach (var batch in urls.Chunk(_config.DetailBatchSize))
        {
            ct.ThrowIfCancellationRequested();

            foreach (var url in batch)
            {
                ct.ThrowIfCancellationRequested();
                int n = done + failed + 1;

                try
                {
                    var detail = await _detailCrawler.CrawlAsync(url, ct);
                    if (detail is not null)
                    {
                        await _storage.SaveDetailAsync(detail);

                        // ثبت قیمت دقیق از صفحه Detail (اگر تغییر کرده)
                        if (detail.PackagePrice > 0)
                            await _storage.SavePricesAsync([DetailToPriceRecord(detail)]);

                        done++;
                        Log($"[{n}/{urls.Count}] ✓ {detail.WebId}  {detail.BrandName}");
                    }
                    else
                    {
                        failed++;
                        Log($"[{n}/{urls.Count}] ✗ null — {url}");
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    Log($"[{n}/{urls.Count}] ✗ ERROR: {ex.Message}");
                }

                OnProgress?.Invoke(new CrawlProgress(CrawlPhase.Detail, n, urls.Count, done));
                await Task.Delay(_config.DetailDelayMs, ct);
            }
        }

        Log($"✅ Phase 2 done — success:{done}  failed:{failed}");
    }

    // ── Price Update ──────────────────────────────────────────

    /// <summary>
    /// فقط قیمت‌ها را بروز می‌کند — از نتایج سرچ (بدون ورود به صفحه Detail).
    /// فقط برای داروهایی که از قبل در دیتابیس هستند.
    /// قیمت واحد از روی قیمت بسته و تعداد محاسبه می‌شود.
    /// </summary>
    public async Task RunPriceUpdateAsync(
        IEnumerable<string> terms,
        CancellationToken ct = default)
    {
        await _storage.InitializeAsync();

        var list = terms.ToList();
        int totalUpdated = 0;
        int i = 0;

        Log($"💰 Price Update — {list.Count} terms");

        foreach (var term in list)
        {
            ct.ThrowIfCancellationRequested();
            i++;

            try
            {
                var drugs = await _searchCrawler.CrawlAsync(term, ct);
                // SaveSearchResultsAsync = نادیده گرفتن داروهای جدید (INSERT OR IGNORE)
                // فقط قیمت‌ها مهم هستند
                int updated = await _storage.SavePricesAsync(ToPriceRecords(drugs, "search"));
                totalUpdated += updated;

                Log($"[{i}/{list.Count}] {term,-10} → found:{drugs.Count,-5} Δprice:{updated}");
            }
            catch (Exception ex)
            {
                Log($"[{i}/{list.Count}] {term,-10} → ERROR: {ex.Message}");
            }

            OnProgress?.Invoke(new CrawlProgress(CrawlPhase.PriceUpdate, i, list.Count, totalUpdated));
            if (i < list.Count) await Task.Delay(_config.SearchDelayMs, ct);
        }

        Log($"✅ Price Update done — {totalUpdated} price changes recorded");
    }

    // ── Full Pipelines ────────────────────────────────────────

    /// <summary>Phase 1 + Phase 2 — کراول کامل</summary>
    public async Task RunAsync(IEnumerable<string> searchTerms, CancellationToken ct = default)
    {
        await RunSearchPhaseAsync(searchTerms, ct);
        await PrintStats();
        await RunDetailPhaseAsync(ct);
        await PrintStats();
    }

    // ── Helpers ───────────────────────────────────────────────

    private static IEnumerable<PriceRecord> ToPriceRecords(
        IEnumerable<DrugBasic> drugs,
        string source) =>
        drugs
            .Where(d => d.WebId > 0 && d.Price > 0)
            .Select(d => new PriceRecord
            {
                WebId = d.WebId,
                PackagePrice = d.Price,
                UnitCount = PackagingParser.ExtractUnitCount(d.Packaging),
                UnitPrice = PackagingParser.CalculateUnitPrice(d.Price, d.Packaging),
                Source = source
            });

    private static PriceRecord DetailToPriceRecord(DrugDetail d) => new()
    {
        WebId = d.WebId,
        PackagePrice = d.PackagePrice,
        UnitPrice = d.UnitPrice,
        // UnitCount محاسبه‌شده از روی Packaging (اگر UnitPrice در دسترس نبود)
        UnitCount = d.UnitPrice > 0
                       ? (int)(d.PackagePrice / d.UnitPrice)
                       : PackagingParser.ExtractUnitCount(d.Packaging),
        Source = "detail"
    };

    private async Task PrintStats()
    {
        var (total, withDetail) = await _storage.GetStatsAsync();
        Log($"📊 DB — total:{total}  with detail:{withDetail}");
    }

    private void Log(string msg) =>
        OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] {msg}");
}

// ── Supporting types ──────────────────────────────────────────

public enum CrawlPhase { Search, Detail, PriceUpdate }

public record CrawlProgress(
    CrawlPhase Phase,
    int Current,
    int Total,
    int Saved)
{
    public double Percent => Total == 0 ? 0 : (double)Current / Total * 100;
}