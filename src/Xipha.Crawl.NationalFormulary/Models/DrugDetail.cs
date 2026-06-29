namespace Xipha.Crawl.NationalFormulary.Models;

/// <summary>
/// Full information for a drug scraped from its Detail page.
/// Clinical fields (indications, side effects, etc.) are not stored — they are
/// identical across all brands sharing the same generic and would bloat the database.
/// Prices exist on this model (for transfer to PriceHistory) but are not persisted
/// in the DrugDetails table.
/// </summary>
public class DrugDetail
{
    // ── Identity ──────────────────────────────────────────────
    public int WebId { get; set; }
    public string DetailUrl { get; set; } = "";

    // ── Core properties ───────────────────────────────────────
    public string BrandName { get; set; } = "";      // name
    public string GenericName { get; set; } = "";    // generic name
    public string DrugForm { get; set; } = "";       // dosage form
    public string RouteOfAdmin { get; set; } = "";   // route of administration
    public string LicenseHolder { get; set; } = "";  // license holder
    public string BrandOwner { get; set; } = "";     // brand owner
    public string Manufacturer { get; set; } = "";   // manufacturer
    public string LicenseExpiry { get; set; } = "";  // license expiry date

    // ── Price (model-only — persisted in PriceHistory) ────────
    public long PackagePrice { get; set; }
    public long UnitPrice { get; set; }

    // ── Codes ─────────────────────────────────────────────────
    public string GTIN { get; set; } = "";
    public string IRC { get; set; } = "";
    public string Packaging { get; set; } = "";      // units per package

    // ── Composition & ATC ─────────────────────────────────────
    public string Composition { get; set; } = "";
    public string ATCCode { get; set; } = "";
    public List<ATCLevel> ATCHierarchy { get; set; } = [];

    public DateTime ScrapedAt { get; set; } = DateTime.UtcNow;
}

public record ATCLevel(string Code, string Description);