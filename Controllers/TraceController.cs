using HotelGraphApi.Models.Dtos;
using HotelGraphApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace HotelGraphApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TraceController : ControllerBase
{
    private readonly GuestTraceService _traceService;

    public TraceController(GuestTraceService traceService)
    {
        _traceService = traceService;
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<SearchResultDto>>> Search(
        [FromQuery] string q,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { message = "Query parameter 'q' is required." });
        }

        try
        {
            var results = await _traceService.SearchAsync(q.Trim(), cancellationToken);
            return Ok(results);
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { message = "Database unavailable.", detail = ex.Message });
        }
    }

    [HttpGet("reversals")]
    public async Task<ActionResult<IReadOnlyList<ReversalChainDto>>> GetReversalChains(CancellationToken cancellationToken)
    {
        try
        {
            var chains = await _traceService.GetReversalChainsAsync(cancellationToken);
            return Ok(chains);
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { message = "Database unavailable.", detail = ex.Message });
        }
    }

    [HttpGet("shared-reference")]
    public async Task<ActionResult<IReadOnlyList<SearchResultDto>>> GetSharedReferences(
        [FromQuery] string reference,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return BadRequest(new { message = "Query parameter 'reference' is required." });
        }

        try
        {
            var links = await _traceService.GetSharedReferenceLinksAsync(reference.Trim(), cancellationToken);
            return Ok(links);
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { message = "Database unavailable.", detail = ex.Message });
        }
    }
}
