namespace Xipha.Crawl.Models
{

    /// <summary>
    /// یک رکورد قیمت — هر بار که قیمت تغییر کند یه ردیف جدید اضافه می‌شود.
    /// </summary>
    public class PriceRecord
    {
        public int WebId { get; set; }
        public long PackagePrice { get; set; }  // قیمت هر بسته (از سایت)
        public long UnitPrice { get; set; }  // قیمت واحد (محاسبه‌شده یا از Detail)
        public int UnitCount { get; set; }  // تعداد در بسته
        public string Source { get; set; } = "search"; // "search" | "detail"
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    }
}