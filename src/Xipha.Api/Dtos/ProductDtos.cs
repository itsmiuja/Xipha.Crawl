namespace Xipha.Api.Dtos;

public record ProductDto(
    int Id, int WebId, string PersianName, string EnglishName, string BrandOwner,
    string LicenseHolder, string Packaging, string ProductCode, string GenericCode,
    string DetailUrl, string SearchTermUsed, bool IsEmergencyLicense,
    bool IsDetailScraped, DateTime ScrapedAt);

public record CreateProductRequest(
    string PersianName, string EnglishName, string BrandOwner, string LicenseHolder,
    string Packaging, string ProductCode, string GenericCode, string DetailUrl,
    bool IsEmergencyLicense);

public record UpdateProductRequest(
    string PersianName, string EnglishName, string BrandOwner, string LicenseHolder,
    string Packaging, string ProductCode, string GenericCode, string DetailUrl,
    bool IsEmergencyLicense);

public record PagedResult<T>(List<T> Items, int TotalCount, int Skip, int Take);