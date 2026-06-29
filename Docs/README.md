# Xipha.Crawl — Documentation

## Overview

**Xipha.Crawl** is a .NET 8 web crawler that collects drug data from Iran's official FDA registry
([irc.fda.gov.ir](https://irc.fda.gov.ir)) and stores it in a local SQLite database.

Primary goals:
- **Collect basic drug information** (name, codes, license holder, ...)
- **Extract clinical and ATC data** from individual drug detail pages
- **Track price history** — every price change is recorded as a new row

---

## Documentation Index

| File | Topic |
|---|---|
| [architecture.md](./architecture.md) | System layers and data flow |
| [pipeline.md](./pipeline.md) | Execution pipeline — Phase 1, Phase 2, Price Update |
| [crawlers.md](./crawlers.md) | HTTP crawlers and retry logic |
| [parsers.md](./parsers.md) | HTML parsers |
| [data-models.md](./data-models.md) | Data models |
| [storage.md](./storage.md) | SQLite storage layer |
| [search-terms.md](./search-terms.md) | Search keyword strategy |

---

## High-Level Flow

```
SearchTerms (~230 prefixes)
        │
        ▼
  SearchCrawler          ←── HTTP GET /NFI/Search?Term=...
        │
        ▼
 SearchResultParser      ←── parse HTML, extract DrugBasic list
        │
        ├──► SaveSearchResultsAsync   → Drugs table        (INSERT OR IGNORE)
        ├──► SavePricesAsync          → PriceHistory table (only if price changed)
        │
        ▼ (new drugs only)
  DetailCrawler          ←── HTTP GET /NFI/Detail/{id}
        │
        ▼
 DetailPageParser        ←── parse HTML, extract DrugDetail
        │
        ├──► SaveDetailAsync          → DrugDetails table
        └──► SavePricesAsync          → PriceHistory table (more precise price)
```

---

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| `HtmlAgilityPack` | 1.12.4 | HTML parsing |
| `Microsoft.Data.Sqlite` | 10.0.9 | Database |

---

## Configuration (CrawlerConfig)

| Parameter | Default | Description |
|---|---|---|
| `BaseUrl` | `https://irc.fda.gov.ir` | Target site base URL |
| `SearchDelayMs` | `1800` ms | Delay between Search requests |
| `DetailDelayMs` | `1200` ms | Delay between Detail requests |
| `RetryDelayMs` | `5000` ms | Base retry delay (multiplied by attempt number) |
| `PageSize` | `10000` | Results per search page |
| `DetailBatchSize` | `10` | Batch size for Detail phase |
| `MaxRetries` | `3` | Maximum retry attempts per request |
