namespace Xipha.Crawl.Models;

/// <summary>
/// اطلاعات کامل یک دارو از صفحه Detail.
/// فیلدهای بالینی (موارد مصرف، عوارض و ...) ذخیره نمی‌شوند — تکراری هستند بین جنریک‌های یکسان.
/// قیمت‌ها در این مدل هستند (برای انتقال به PriceHistory) اما در جدول DrugDetails ذخیره نمی‌شوند.
/// </summary>
public class DrugDetail
{
    // ── شناسه ────────────────────────────────────────────────
    public int WebId { get; set; }
    public string DetailUrl { get; set; } = "";

    // ── مشخصات اصلی ──────────────────────────────────────────
    public string BrandName { get; set; } = "";   // نام
    public string GenericName { get; set; } = "";   // نام عمومی
    public string DrugForm { get; set; } = "";   // شکل دارویی
    public string RouteOfAdmin { get; set; } = "";   // نحوه مصرف
    public string LicenseHolder { get; set; } = "";   // صاحب پروانه
    public string BrandOwner { get; set; } = "";   // صاحب برند
    public string Manufacturer { get; set; } = "";   // تولید کننده
    public string LicenseExpiry { get; set; } = "";   // تاریخ اعتبار پروانه

    // ── قیمت (فقط در مدل — در PriceHistory ذخیره می‌شود) ────
    public long PackagePrice { get; set; }
    public long UnitPrice { get; set; }

    // ── کدها ─────────────────────────────────────────────────
    public string GTIN { get; set; } = "";
    public string IRC { get; set; } = "";
    public string Packaging { get; set; } = "";       // تعداد در بسته

    // ── ترکیب و ATC ──────────────────────────────────────────
    public string Composition { get; set; } = "";
    public string ATCCode { get; set; } = "";
    public List<ATCLevel> ATCHierarchy { get; set; } = [];

    public DateTime ScrapedAt { get; set; } = DateTime.UtcNow;
}

public record ATCLevel(string Code, string Description);