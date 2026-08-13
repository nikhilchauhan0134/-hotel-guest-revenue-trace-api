using HotelGraphApi.Models.Dtos;
using HotelGraphApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace HotelGraphApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GuestsController : ControllerBase
{
    private readonly GuestTraceService _traceService;

    public GuestsController(GuestTraceService traceService)
    {
        _traceService = traceService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GuestSummaryDto>>> GetGuests(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        try
        {
            var guests = await _traceService.GetGuestsAsync(search, cancellationToken);
            return Ok(guests);
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { message = "Database unavailable.", detail = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<GuestSummaryDto>> RegisterGuest(
        [FromBody] RegisterGuestRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Guest name is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            return BadRequest(new { message = "Phone number is required." });
        }

        try
        {
            var (guest, error) = await _traceService.RegisterGuestAsync(request, cancellationToken);
            if (error is not null)
            {
                return Conflict(new { message = error });
            }

            return CreatedAtAction(nameof(GetGuest), new { guestId = guest!.Id }, guest);
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { message = "Database unavailable.", detail = ex.Message });
        }
    }

    [HttpGet("{guestId}")]
    public async Task<ActionResult<GuestDetailDto>> GetGuest(string guestId, CancellationToken cancellationToken)
    {
        try
        {
            var guest = await _traceService.GetGuestByIdAsync(guestId, cancellationToken);
            if (guest is null)
            {
                var available = await _traceService.GetGuestsAsync(null, cancellationToken);
                var names = string.Join(", ", available.Select(g => g.Name));
                return NotFound(new
                {
                    message = $"Guest '{guestId}' not found in the database.",
                    hint = "This app uses seed data only. Available guests: " + names,
                    availableGuests = available
                });
            }

            return Ok(guest);
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { message = "Database unavailable.", detail = ex.Message });
        }
    }

    [HttpGet("{guestId}/trace")]
    public async Task<ActionResult<RevenueTraceDto>> GetGuestTrace(string guestId, CancellationToken cancellationToken)
    {
        try
        {
            var trace = await _traceService.GetGuestRevenueTraceAsync(guestId, cancellationToken);
            return trace is null ? NotFound(new { message = $"Guest '{guestId}' not found." }) : Ok(trace);
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { message = "Database unavailable.", detail = ex.Message });
        }
    }

    [HttpGet("{guestId}/ledger")]
    public async Task<ActionResult<IReadOnlyList<LedgerEntryDto>>> GetGuestLedger(
        string guestId,
        CancellationToken cancellationToken)
    {
        try
        {
            var ledger = await _traceService.GetReservationLedgerAsync(guestId, cancellationToken);
            return Ok(ledger);
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { message = "Database unavailable.", detail = ex.Message });
        }
    }
}
