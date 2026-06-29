using HtmlAgilityPack;
using Xipha.Crawl.Models;

namespace Xipha.Crawl.Parsers;

public class DetailPageParser
{
    private const string BaseUrl = "https://irc.fda.gov.ir";

    public DrugDetail Parse(string html, string detailUrl)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var drug = new DrugDetail { DetailUrl = detailUrl };

        // WebId از URL
        var seg = detailUrl.TrimEnd('/').Split('/');
        if (int.TryParse(seg[^1], out int id)) drug.WebId = id;

        // ── مشخصات اصلی ──────────────────────────────────────
        drug.BrandName = ByLabel(doc, "نام :");
        drug.GenericName = ByLabelBdo(doc, "نام عمومی");
        drug.DrugForm = ByLabel(doc, "شکل دارویی");
        drug.RouteOfAdmin = ByLabelBdo(doc, "نحوه مصرف");
        drug.LicenseHolder = ByLabelClass(doc, "صاحب پروانه", "txtSearch1");
        drug.BrandOwner = BrandOwnerClean(doc);
        drug.Manufacturer = ByLabel(doc, "تولید کننده");
        drug.LicenseExpiry = ByLabel(doc, "تاریخ اعتبار پروانه");
        drug.Packaging = ByLabelBdo(doc, "تعداد در بسته");
        drug.Composition = ByLabel(doc, "ترکیبات");

        // ── قیمت‌ها ───────────────────────────────────────────
        var prices = doc.DocumentNode.SelectNodes("//span[contains(@class,'priceTxt')]");
        if (prices?.Count >= 1) drug.PackagePrice = ParsePrice(prices[0].InnerText);
        if (prices?.Count >= 2) drug.UnitPrice = ParsePrice(prices[1].InnerText);

        // ── کدها ─────────────────────────────────────────────
        drug.GTIN = ByLabelEng(doc, ":GTIN");
        drug.IRC = ByLabelEng(doc, ":IRC");

        // ── ATC ──────────────────────────────────────────────
        drug.ATCCode = doc.DocumentNode
            .SelectSingleNode("//label[@class='graphLabelSearch']")
            ?.InnerText.Trim() ?? "";
        drug.ATCHierarchy = ParseATCHierarchy(doc);

        // فیلدهای بالینی (موارد مصرف، عوارض و ...) استخراج نمی‌شوند
        // چون بین تمام داروهای یک جنریک تکراری‌اند و حجم را بالا می‌برند.

        return drug;
    }

    // ── Helpers ───────────────────────────────────────────────

    private static string ByLabel(HtmlDocument doc, string label) =>
        doc.DocumentNode
           .SelectSingleNode($"//label[contains(text(),'{label}')]/following-sibling::span[1]")
           ?.InnerText.Trim() ?? "";

    private static string ByLabelBdo(HtmlDocument doc, string label) =>
        doc.DocumentNode
           .SelectSingleNode($"//label[contains(text(),'{label}')]/following-sibling::bdo[1]")
           ?.InnerText.Trim() ?? "";

    private static string ByLabelClass(HtmlDocument doc, string label, string cls) =>
        doc.DocumentNode
           .SelectSingleNode(
               $"//label[contains(text(),'{label}')]" +
               $"/following-sibling::span[contains(@class,'{cls}')]")
           ?.InnerText.Trim() ?? "";

    private static string ByLabelEng(HtmlDocument doc, string label) =>
        doc.DocumentNode
           .SelectSingleNode(
               $"//label[contains(@class,'txtSearchEnglish-ltr') and contains(text(),'{label}')]" +
               "/following-sibling::span[1]")
           ?.InnerText.Trim() ?? "";

    private static string BrandOwnerClean(HtmlDocument doc)
    {
        var node = doc.DocumentNode
            .SelectSingleNode("//label[contains(text(),'صاحب برند')]/following-sibling::span[1]");
        if (node is null) return "";
        return string.Concat(
            node.ChildNodes
                .Where(n => n.NodeType == HtmlNodeType.Text)
                .Select(n => n.InnerText))
            .Trim();
    }

    private static List<ATCLevel> ParseATCHierarchy(HtmlDocument doc)
    {
        var result = new List<ATCLevel>();
        var boxes = doc.DocumentNode.SelectNodes("//div[contains(@class,'graphBox')]");
        if (boxes is null) return result;

        foreach (var box in boxes)
        {
            var desc = box
                .SelectSingleNode(".//div[contains(@class,'colorMediumPurple')]/a")
                ?.InnerText.Trim();
            var code = box
                .SelectSingleNode(".//span[contains(@class,'txtEnglish-ltr1')]/a")
                ?.InnerText.Trim();

            if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(desc))
                result.Add(new ATCLevel(code, desc));
        }
        return result;
    }

    private static long ParsePrice(string text) =>
        long.TryParse(text.Replace(",", "").Trim(), out long p) ? p : 0;
}