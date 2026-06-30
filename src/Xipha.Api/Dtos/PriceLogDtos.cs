namespace Xipha.Api.Dtos;

public record PriceLogDto(
    int Id, int WebId, long PackagePrice, long UnitPrice, int UnitCount,
    string Source, DateTime RecordedAt);

public record CreatePriceLogRequest(
    long PackagePrice, long UnitPrice, int UnitCount, string? Source);

public record UpdatePriceLogRequest(
    long PackagePrice, long UnitPrice, int UnitCount, string? Source);