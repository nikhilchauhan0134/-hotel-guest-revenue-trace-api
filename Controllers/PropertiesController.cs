using HotelGraphApi.Models.Dtos;
using HotelGraphApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace HotelGraphApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PropertiesController : ControllerBase
{
    private readonly GuestTraceService _traceService;

    public PropertiesController(GuestTraceService traceService)
    {
        _traceService = traceService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PropertyStatsDto>>> GetProperties(CancellationToken cancellationToken)
    {
        try
        {
            var properties = await _traceService.GetPropertyStatsAsync(cancellationToken);
            return Ok(properties);
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { message = "Database unavailable.", detail = ex.Message });
        }
    }
}
