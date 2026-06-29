# Pipeline — Execution Flow

## Three Execution Modes

`CrawlPipeline` exposes three independent operations:

| Method | Purpose |
|---|---|
| `RunAsync()` | Full crawl — Phase 1 + Phase 2 |
| `RunSearchPhaseAsync()` | Search and register new drugs only |
| `RunDetailPhaseAsync()` | Fetch detail pages for new drugs from this session |
| `RunPriceUpdateAsync()` | Update prices only — no new drugs, no detail pages |

---

## Phase 1 — Search

```
RunSearchPhaseAsync(terms)
│
├─► InitializeAsync()               ← create tables if they don't exist
│
└─► foreach term in terms:
      │
      ├─► SearchCrawler.CrawlAsync(term)
      │         └── up to MaxRetries attempts, with RetryDelayMs × attempt delay
      │
      ├─► SaveSearchResultsAsync(drugs)
      │         └── INSERT OR IGNORE → only truly new drugs are inserted
      │         └── returns: List<int> newWebIds
      │
      ├─► _sessionNewWebIds.AddRange(newWebIds)
      │
      ├─► SavePricesAsync(ToPriceRecords(drugs, "search"))
      │         └── records prices for all found drugs (new and existing)
      │         └── only writes if price has changed since last record
      │
      └─► Task.Delay(SearchDelayMs)   ← respects site rate limits
```

**Output of Phase 1:**
- `Drugs` table populated
- `PriceHistory` table updated
- `_sessionNewWebIds` contains freshly inserted WebIds

---

## Phase 2 — Detail

```
RunDetailPhaseAsync()
│
├─► GetDetailUrlsByWebIdsAsync(_sessionNewWebIds)
│         └── only URLs where IsDetailScraped = 0
│
└─► foreach batch in urls.Chunk(DetailBatchSize):
      └─► foreach url in batch:
            │
            ├─► DetailCrawler.CrawlAsync(url)
            │
            ├─► SaveDetailAsync(detail)
            │         ├── INSERT OR REPLACE → DrugDetails table
            │         └── UPDATE Drugs SET IsDetailScraped = 1
            │
            ├─► SavePricesAsync([DetailToPriceRecord(detail)])
            │         └── more precise price from the detail page
            │
            └─► Task.Delay(DetailDelayMs)
```

**Important:** If a drug was already in the database before this run (not in `_sessionNewWebIds`), Phase 2 will **not** re-crawl its detail page. To force a re-crawl, manually set `IsDetailScraped = 0`.

---

## Price Update

```
RunPriceUpdateAsync(terms)
│
└─► foreach term in terms:
      │
      ├─► SearchCrawler.CrawlAsync(term)
      │
      └─► SavePricesAsync(ToPriceRecords(drugs, "search"))
            └── SaveSearchResultsAsync is NOT called
            └── new drugs are ignored (WebId existence check in SQL)
```

This is the fastest and lightest operation — only prices are checked and recorded.

---

## Price Conversion: ToPriceRecords

When converting search results to `PriceRecord`:

```csharp
// Uses PackagingParser.ExtractUnitCount()
// e.g. "20 TABLET in 2 BLISTER PACK" → UnitCount = 20
// UnitPrice = PackagePrice / UnitCount

new PriceRecord {
    WebId        = d.WebId,
    PackagePrice = d.Price,
    UnitCount    = PackagingParser.ExtractUnitCount(d.Packaging),
    UnitPrice    = PackagingParser.CalculateUnitPrice(d.Price, d.Packaging),
    Source       = "search"
}
```

When converting from a detail page:

```csharp
// UnitPrice is read directly from the HTML
// UnitCount = PackagePrice / UnitPrice (when UnitPrice is available)

new PriceRecord {
    WebId        = d.WebId,
    PackagePrice = d.PackagePrice,
    UnitPrice    = d.UnitPrice,
    UnitCount    = d.UnitPrice > 0
                     ? (int)(d.PackagePrice / d.UnitPrice)
                     : PackagingParser.ExtractUnitCount(d.Packaging),
    Source       = "detail"
}
```

Prices with `Source = "detail"` are more accurate since they are read directly from the site.

---

## Events and Progress Reporting

The pipeline exposes two events:

```csharp
pipeline.OnLog += msg => Console.WriteLine(msg);

pipeline.OnProgress += progress => {
    // progress.Phase    → Search | Detail | PriceUpdate
    // progress.Current  → number of items processed so far
    // progress.Total    → total items to process
    // progress.Percent  → completion percentage
    // progress.Saved    → number of records saved
};
```
