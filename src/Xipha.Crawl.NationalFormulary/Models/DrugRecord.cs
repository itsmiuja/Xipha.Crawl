namespace Xipha.Crawl.NationalFormulary.Models;

/// <summary>
/// Full row from the Drugs table, including the auto-generated Id and scrape metadata.
/// Used for manual CRUD operations exposed via the REST API.
/// </summary>
public class DrugRecord
{
    public int Id { get; set; }
    public int WebId { get; set; }
    public string PersianName { get; set; } = "";
    public string EnglishName { get; set; } = "";
    public string BrandOwner { get; set; } = "";
    public string LicenseHolder { get; set; } = "";
    public string Packaging { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public string GenericCode { get; set; } = "";
    public string DetailUrl { get; set; } = "";
    public string SearchTermUsed { get; set; } = "";
    public bool IsEmergencyLicense { get; set; }
    public bool IsDetailScraped { get; set; }
    public DateTime ScrapedAt { get; set; }
}