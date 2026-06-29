using Xipha.Crawl.Models;

namespace Xipha.Crawl.Storage;

public interface IStorage
{
    /// <summary>ساخت جداول در صورت نبود</summary>
    Task InitializeAsync();

    /// <summary>
    /// ذخیره نتایج جستجو — رکوردهای تکراری نادیده گرفته می‌شوند.
    /// برمی‌گرداند: WebId های جدید اضافه‌شده در این فراخوانی
    /// </summary>
    Task<List<int>> SaveSearchResultsAsync(IEnumerable<DrugBasic> drugs);

    /// <summary>ذخیره جزئیات کامل یک دارو + علامت‌گذاری IsDetailScraped</summary>
    Task<int> SaveDetailAsync(DrugDetail detail);

    /// <summary>
    /// ذخیره قیمت — فقط اگر قیمت نسبت به آخرین رکورد تغییر کرده باشد.
    /// فقط برای داروهایی که در جدول Drugs موجودند.
    /// </summary>
    Task<int> SavePricesAsync(IEnumerable<PriceRecord> records);

    /// <summary>DetailUrl داروهای مشخص‌شده که هنوز IsDetailScraped = 0 دارند</summary>
    Task<IEnumerable<string>> GetDetailUrlsByWebIdsAsync(IEnumerable<int> webIds);

    /// <summary>آمار کلی: (کل رکوردها، تعداد با Detail)</summary>
    Task<(int Total, int WithDetail)> GetStatsAsync();
}