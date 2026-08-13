using HotelGraphApi.Models.Dtos;
using HotelGraphApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace HotelGraphApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IGraphDatabaseService _graph;

    public HealthController(IGraphDatabaseService graph)
    {
        _graph = graph;
    }

    [HttpGet]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        var connected = await _graph.VerifyConnectivityAsync(cancellationToken);
        return Ok(new HealthResponse(
            connected,
            connected ? "CognoDB is reachable." : "Unable to reach CognoDB. Check COGNODB_URI and COGNODB_PASSWORD."));
    }
}
