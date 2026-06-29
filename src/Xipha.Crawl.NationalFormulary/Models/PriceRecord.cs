namespace Xipha.Crawl.NationalFormulary.Models
{
    /// <summary>
    /// A price snapshot — a new row is inserted every time the price changes.
    /// </summary>
    public class PriceRecord
    {
        public int WebId { get; set; }
        public long PackagePrice { get; set; }  // price per package (from site)
        public long UnitPrice { get; set; }     // per-unit price (calculated or from detail page)
        public int UnitCount { get; set; }      // units per package
        public string Source { get; set; } = "search"; // "search" | "detail"
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    }
}