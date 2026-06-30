using Microsoft.AspNetCore.Mvc;
using Xipha.Api.Dtos;
using Xipha.Crawl.NationalFormulary.Models;
using Xipha.Crawl.NationalFormulary.Storage;

namespace Xipha.Api.Controllers;

[ApiController]
[Route("api/products/{webId:int}/prices")]
public class PriceLogController : ControllerBase
{
    private readonly IStorage _storage;
    public PriceLogController(IStorage storage) => _storage = storage;

    [HttpGet]
    public async Task<ActionResult<PagedResult<PriceLogDto>>> GetHistory(
        int webId, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        take = Math.Clamp(take, 1, 200);
        var (items, total) = await _storage.GetPriceHistoryAsync(webId, skip, take);
        return Ok(new PagedResult<PriceLogDto>(items.Select(ToDto).ToList(), total, skip, take));
    }

    [HttpPost]
    public async Task<ActionResult<PriceLogDto>> Create(int webId, CreatePriceLogRequest req)
    {
        var record = new PriceRecord
        {
            WebId = webId,
            PackagePrice = req.PackagePrice,
            UnitPrice = req.UnitPrice,
            UnitCount = req.UnitCount,
            Source = req.Source ?? "manual",
            RecordedAt = DateTime.UtcNow
        };

        var newId = await _storage.CreatePriceRecordManualAsync(record);
        if (newId is null) return NotFound("No product with this WebId.");

        var created = await _storage.GetPriceRecordByIdAsync(newId.Value);
        return CreatedAtAction(nameof(GetHistory), new { webId }, ToDto(created!));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int webId, int id, UpdatePriceLogRequest req)
    {
        var record = new PriceRecord
        {
            WebId = webId,
            PackagePrice = req.PackagePrice,
            UnitPrice = req.UnitPrice,
            UnitCount = req.UnitCount,
            Source = req.Source ?? "manual"
        };

        return await _storage.UpdatePriceRecordAsync(id, record) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int webId, int id) =>
        await _storage.DeletePriceRecordAsync(id) ? NoContent() : NotFound();

    private static PriceLogDto ToDto(PriceRecord r) => new(
        r.Id, r.WebId, r.PackagePrice, r.UnitPrice, r.UnitCount, r.Source, r.RecordedAt);
}