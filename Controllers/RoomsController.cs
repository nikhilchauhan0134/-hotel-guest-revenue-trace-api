using HotelGraphApi.Models.Dtos;
using HotelGraphApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace HotelGraphApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    private readonly GuestTraceService _traceService;

    public RoomsController(GuestTraceService traceService)
    {
        _traceService = traceService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoomListItemDto>>> GetRooms(
        [FromQuery] string? propertyCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var rooms = await _traceService.GetRoomsAsync(propertyCode, cancellationToken);
            return Ok(rooms);
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { message = "Database unavailable.", detail = ex.Message });
        }
    }
}
