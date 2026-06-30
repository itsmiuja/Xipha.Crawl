using Microsoft.AspNetCore.Mvc;
using Xipha.Api.Dtos;
using Xipha.Crawl.NationalFormulary.Models;
using Xipha.Crawl.NationalFormulary.Storage;

namespace Xipha.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IStorage _storage;
    public ProductsController(IStorage storage) => _storage = storage;

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetAll(
        [FromQuery] int skip = 0, [FromQuery] int take = 50, [FromQuery] string? search = null)
    {
        take = Math.Clamp(take, 1, 200);
        var (items, total) = await _storage.GetDrugsAsync(skip, take, search);
        return Ok(new PagedResult<ProductDto>(items.Select(ToDto).ToList(), total, skip, take));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> GetById(int id)
    {
        var drug = await _storage.GetDrugByIdAsync(id);
        return drug is null ? NotFound() : Ok(ToDto(drug));
    }

    [HttpGet("{id:int}/detail")]
    public async Task<ActionResult<DrugDetail>> GetDetail(int id)
    {
        var drug = await _storage.GetDrugByIdAsync(id);
        if (drug is null) return NotFound();

        var detail = await _storage.GetDrugDetailByWebIdAsync(drug.WebId);
        return detail is null ? NotFound("Detail not yet scraped for this product.") : Ok(detail);
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(CreateProductRequest req)
    {
        var drug = new DrugBasic
        {
            PersianName = req.PersianName,
            EnglishName = req.EnglishName,
            BrandOwner = req.BrandOwner,
            LicenseHolder = req.LicenseHolder,
            Packaging = req.Packaging,
            ProductCode = req.ProductCode,
            GenericCode = req.GenericCode,
            DetailUrl = req.DetailUrl,
            SearchTermUsed = "manual",
            IsEmergencyLicense = req.IsEmergencyLicense
        };

        var newId = await _storage.CreateDrugAsync(drug);
        if (newId is null) return Conflict("A product with this WebId/DetailUrl already exists.");

        var created = await _storage.GetDrugByIdAsync(newId.Value);
        return CreatedAtAction(nameof(GetById), new { id = newId }, ToDto(created!));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateProductRequest req)
    {
        var drug = new DrugBasic
        {
            PersianName = req.PersianName,
            EnglishName = req.EnglishName,
            BrandOwner = req.BrandOwner,
            LicenseHolder = req.LicenseHolder,
            Packaging = req.Packaging,
            ProductCode = req.ProductCode,
            GenericCode = req.GenericCode,
            DetailUrl = req.DetailUrl,
            IsEmergencyLicense = req.IsEmergencyLicense
        };

        return await _storage.UpdateDrugAsync(id, drug) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id) =>
        await _storage.DeleteDrugAsync(id) ? NoContent() : NotFound();

    private static ProductDto ToDto(DrugRecord d) => new(
        d.Id, d.WebId, d.PersianName, d.EnglishName, d.BrandOwner, d.LicenseHolder,
        d.Packaging, d.ProductCode, d.GenericCode, d.DetailUrl, d.SearchTermUsed,
        d.IsEmergencyLicense, d.IsDetailScraped, d.ScrapedAt);
}