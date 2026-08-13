using HotelGraphApi.Models.Dtos;
using HotelGraphApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace HotelGraphApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeedController : ControllerBase
{
    private readonly SeedService _seedService;

    public SeedController(SeedService seedService)
    {
        _seedService = seedService;
    }

    [HttpPost]
    public async Task<ActionResult<SeedResultDto>> Seed(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _seedService.SeedAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { message = "Failed to seed database.", detail = ex.Message });
        }
    }
}
