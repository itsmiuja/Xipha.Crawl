using System.Text.RegularExpressions;

namespace Xipha.Crawl.Parsers;

/// <summary>
/// تعداد واحد را از رشته بسته‌بندی استخراج می‌کند.
/// مثال: "20 TABLET in 2 BLISTER PACK in 1 BOX" → 20
/// </summary>
public static class PackagingParser
{
    private static readonly Regex FirstNumber =
        new(@"^(\d+)", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>اولین عدد در رشته بسته‌بندی = تعداد کل واحد</summary>
    public static int ExtractUnitCount(string packaging)
    {
        if (string.IsNullOrWhiteSpace(packaging)) return 1;
        var m = FirstNumber.Match(packaging.Trim());
        return m.Success && int.TryParse(m.Groups[1].Value, out int n) && n > 0 ? n : 1;
    }

    /// <summary>قیمت واحد = قیمت بسته ÷ تعداد</summary>
    public static long CalculateUnitPrice(long packagePrice, string packaging)
    {
        int count = ExtractUnitCount(packaging);
        return count > 1 ? packagePrice / count : packagePrice;
    }
}