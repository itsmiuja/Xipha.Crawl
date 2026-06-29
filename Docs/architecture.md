# Architecture

## System Layers

The system is split into **four independent layers**, each only communicating with the layer directly below it:

```
┌─────────────────────────────────────────────────────────┐
│                    CrawlPipeline                        │  ← orchestrator
├────────────────────┬────────────────────────────────────┤
│  SearchCrawler     │       DetailCrawler                │  ← HTTP layer
├────────────────────┴────────────────────────────────────┤
│  SearchResultParser │ DetailPageParser │ PackagingParser │  ← parsing layer
├─────────────────────────────────────────────────────────┤
│                   SqliteStorage (IStorage)              │  ← data layer
└─────────────────────────────────────────────────────────┘
```

---

## Layer Responsibilities

### 1. Pipeline (Orchestrator)

`CrawlPipeline` is the central coordinator. It is responsible for:
- Sequencing execution phases
- Managing delays between requests (rate limiting)
- Publishing progress events (`OnProgress`) and log events (`OnLog`)
- Maintaining `_sessionNewWebIds` — the list of drugs discovered in the current run

### 2. Crawlers (HTTP)

Two independent crawlers sharing the same retry logic:

- **SearchCrawler**: accepts a search term, returns the raw HTML of the search results page
- **DetailCrawler**: accepts a detail URL, returns the raw HTML of the drug detail page

Both crawlers receive a shared `HttpClient` injected from outside (single instance, connection pooling).

### 3. Parsers (HTML → Model)

- **SearchResultParser**: maps each `div.RowSearchSty` into a `DrugBasic` object
- **DetailPageParser**: maps a full detail page into a `DrugDetail` object
- **PackagingParser**: extracts unit count from a packaging string (e.g. `"20 TABLET in 2 BLISTER"`)

### 4. Storage (SQLite)

The data layer is hidden behind the `IStorage` interface. This means the storage backend can be swapped (e.g. to PostgreSQL) without touching any other layer.

---

## Data Flow

### Search Phase

```
term (string)
    │
    ▼  SearchCrawler.CrawlAsync()
HTML (string)
    │
    ▼  SearchResultParser.Parse()
List<DrugBasic>
    │
    ├──▶ SaveSearchResultsAsync()  →  Drugs table
    └──▶ SavePricesAsync()         →  PriceHistory table
```

### Detail Phase

```
DetailUrl (string)
    │
    ▼  DetailCrawler.CrawlAsync()
HTML (string)
    │
    ▼  DetailPageParser.Parse()
DrugDetail
    │
    ├──▶ SaveDetailAsync()   →  DrugDetails table + sets Drugs.IsDetailScraped = 1
    └──▶ SavePricesAsync()   →  PriceHistory table (more precise price)
```

---

## Key Design Decisions

### Prices are separated from drug records
Prices are **not** stored in `Drugs` or `DrugDetails`. All prices live in `PriceHistory`. This makes it possible to track price changes over time without modifying existing rows.

### INSERT OR IGNORE for search results
If a drug with the same `WebId` already exists, the insert is silently skipped. Re-running the pipeline is always safe — no duplicate data is produced.

### Detail crawling only for new drugs
`_sessionNewWebIds` only holds WebIds that were **newly inserted in the current run**. Drugs that already had their detail page scraped are never re-crawled, unless `IsDetailScraped` is manually reset to 0.

### Price recorded only on change
`SavePricesAsync` uses a subquery to check whether the last recorded price equals the incoming price. If they match, no new row is written.
