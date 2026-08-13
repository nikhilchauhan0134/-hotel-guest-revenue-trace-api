using HotelGraphApi.Configuration;
using Microsoft.Extensions.Options;
using Neo4j.Driver;

namespace HotelGraphApi.Services;

public class GraphDatabaseService : IGraphDatabaseService, IAsyncDisposable
{
    private readonly IDriver _driver;
    private readonly ILogger<GraphDatabaseService> _logger;

    public GraphDatabaseService(IOptions<CognoDbSettings> options, ILogger<GraphDatabaseService> logger)
    {
        _logger = logger;
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.Uri))
        {
            throw new InvalidOperationException(
                "CognoDb URI is not configured. Set COGNODB_URI environment variable.");
        }

        _driver = GraphDatabase.Driver(
            settings.Uri,
            AuthTokens.Basic(settings.Username, settings.Password));
    }

    public async Task<bool> VerifyConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _driver.VerifyConnectivityAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CognoDB connectivity check failed");
            return false;
        }
    }

    public async Task<IReadOnlyList<IRecord>> RunQueryAsync(
        string cypher,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();
        var result = await session.RunAsync(cypher, ConvertParameters(parameters));
        var records = await result.ToListAsync();
        return records;
    }

    public async Task<IRecord?> RunQuerySingleAsync(
        string cypher,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var records = await RunQueryAsync(cypher, parameters, cancellationToken);
        return records.FirstOrDefault();
    }

    public async Task ExecuteWriteAsync(
        string cypher,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(cypher, ConvertParameters(parameters));
        });
    }

    private static Dictionary<string, object>? ConvertParameters(object? parameters)
    {
        if (parameters is null)
        {
            return null;
        }

        return parameters.GetType().GetProperties()
            .ToDictionary(p => p.Name, p => p.GetValue(parameters)!);
    }

    public async ValueTask DisposeAsync()
    {
        await _driver.DisposeAsync();
    }
}
