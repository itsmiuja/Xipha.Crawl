# Crawlers

## SearchCrawler

### Responsibility
Sends a search term (e.g. `"am"` or `"آم"`) as a GET request:

```
GET /NFI/Search?Term={term}&PageNumber=1&PageSize=10000
```

`PageSize=10000` effectively returns all matching results in a single page, eliminating the need for pagination.

### Retry Logic

```
attempt 1 → fails → wait: RetryDelayMs × 1
attempt 2 → fails → wait: RetryDelayMs × 2
attempt 3 → fails → return []   (empty list)
```

Failures on attempts less than `MaxRetries` are caught with a `when` clause, allowing the loop to continue. The final attempt lets the exception propagate, which the outer `try/catch` catches and converts to an empty list.

### HTTP Client Usage
Both crawlers share a single `HttpClient` instance injected from outside. This ensures:
- Proper connection pooling
- `UserAgent` headers are set only once

---

## DetailCrawler

### Responsibility
Accepts a full URL (e.g. `https://irc.fda.gov.ir/NFI/Detail/12345`) and fetches the drug detail page.

### Retry Logic Difference vs SearchCrawler

```csharp
// SearchCrawler: last attempt is NOT caught — propagates as empty list
catch (Exception) when (attempt < _config.MaxRetries)

// DetailCrawler: last attempt IS caught — returns null instead of throwing
catch (Exception) when (attempt < _config.MaxRetries)  // retry
catch (Exception) { return null; }                     // total failure
```

`DetailCrawler` returns `null` on total failure rather than throwing, so the pipeline can log the failure and move on to the next drug.

---

## Comparison

| Feature | SearchCrawler | DetailCrawler |
|---|---|---|
| Input | `string term` | `string detailUrl` |
| Output | `List<DrugBasic>` | `DrugDetail?` |
| Total failure result | Empty list `[]` | `null` |
| URL construction | Yes (built from term) | No (URL passed in directly) |
| Parser used | `SearchResultParser` | `DetailPageParser` |

---

## Rate Limiting

Delays are applied in `CrawlPipeline`, not inside the crawlers themselves. This separation means:
- Crawlers can be used in tests without any delays
- Search and Detail requests can have different delay values

```
Search requests:  once every SearchDelayMs  (default: 1800 ms)
Detail requests:  once every DetailDelayMs  (default: 1200 ms)
Retry waits:      RetryDelayMs × attempt    (default: 5000 ms, 10000 ms)
```
