using Neo4j.Driver;

namespace HotelGraphApi.Services;

public interface IGraphDatabaseService
{
    Task<bool> VerifyConnectivityAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IRecord>> RunQueryAsync(string cypher, object? parameters = null, CancellationToken cancellationToken = default);
    Task<IRecord?> RunQuerySingleAsync(string cypher, object? parameters = null, CancellationToken cancellationToken = default);
    Task ExecuteWriteAsync(string cypher, object? parameters = null, CancellationToken cancellationToken = default);
}
