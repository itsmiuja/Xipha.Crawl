using HtmlAgilityPack;
using Xipha.Crawl.NationalFormulary.Models;

namespace Xipha.Crawl.NationalFormulary.Parsers;

public class SearchResultParser
{
    private const string BaseUrl = "https://irc.fda.gov.ir";

    public List<DrugBasic> Parse(string html, string searchTerm)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var rows = doc.DocumentNode.SelectNodes("//div[contains(@class,'RowSearchSty')]");
        if (rows is null) return [];

        return rows
            .Select(row => ParseRow(row, searchTerm))
            .Where(d => d is not null)
            .Cast<DrugBasic>()
            .ToList();
    }

    private DrugBasic? ParseRow(HtmlNode row, string searchTerm)
    {
        // Persian name + detail URL (required fields)
        var link = row.SelectSingleNode(".//a[contains(@href,'/NFI/Detail/')]");
        if (link is null) return null;

        var drug = new DrugBasic
        {
            SearchTermUsed = searchTerm,
            PersianName = link.InnerText.Trim(),
            DetailUrl = BaseUrl + link.GetAttributeValue("href", "")
        };

        if (string.IsNullOrWhiteSpace(drug.PersianName)) return null;

        // English name
        var eng = row.SelectSingleNode(".//span[contains(@class,'titleSearch-Link-ltrAlter')]");
        if (eng is not null) drug.EnglishName = eng.InnerText.Trim();

        // Labeled fields
        drug.BrandOwner    = GetLabeledValue(row, "صاحب برند");
        drug.LicenseHolder = GetLabeledValue(row, "صاحب پروانه");
        drug.ProductCode   = GetLabeledValue(row, "کد فرآورده");
        drug.GenericCode   = GetLabeledValue(row, "کد ژنریک");

        // Price
        var priceNode = row.SelectSingleNode(".//span[contains(@class,'priceTxt')]");
        if (priceNode is not null)
        {
            string raw = priceNode.InnerText.Replace(",", "").Trim();
            if (long.TryParse(raw, out long price)) drug.Price = price;
        }

        // Packaging
        var pack = row.SelectSingleNode(".//bdo");
        if (pack is not null) drug.Packaging = pack.InnerText.Trim();

        drug.IsEmergencyLicense = row.InnerText.Contains("پروانه فوریتی");

        return drug;
    }

    private static string GetLabeledValue(HtmlNode row, string label)
    {
        var lbl = row.SelectSingleNode($".//label[contains(text(),'{label}')]");
        if (lbl is null) return "";
        var val = lbl.ParentNode.SelectSingleNode(".//span[contains(@class,'txtSearch1')]");
        return val?.InnerText.Trim() ?? "";
    }
}