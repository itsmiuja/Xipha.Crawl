using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xipha.Crawl
{
    public class CrawlerConfig
    {
        public string BaseUrl { get; set; } = "https://irc.fda.gov.ir";
        public string UserAgent { get; set; } = "Mozilla/5.0 (compatible; XiphaCrawler/1.0)";

        // ── تاخیرها ──────────────────────────────────────────────
        public int SearchDelayMs { get; set; } = 1800;
        public int DetailDelayMs { get; set; } = 1200;
        public int RetryDelayMs { get; set; } = 5000;

        // ── حجم ──────────────────────────────────────────────────
        public int PageSize { get; set; } = 10000;
        public int DetailBatchSize { get; set; } = 10;

        // ── تلاش مجدد ────────────────────────────────────────────
        public int MaxRetries { get; set; } = 3;
    }
}
