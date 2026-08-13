using HotelGraphApi.Models.Dtos;
using Neo4j.Driver;

namespace HotelGraphApi.Services;

public class GuestTraceService
{
    private readonly IGraphDatabaseService _graph;

    public GuestTraceService(IGraphDatabaseService graph)
    {
        _graph = graph;
    }

    public async Task<IReadOnlyList<GuestSummaryDto>> GetGuestsAsync(string? search, CancellationToken cancellationToken = default)
    {
        const string cypher = """
            MATCH (g:Guest)
            WHERE $search IS NULL OR $search = ''
               OR toLower(g.name) CONTAINS toLower($search)
               OR g.id CONTAINS $search
            OPTIONAL MATCH (g)-[:BOOKED]->(r:Reservation)
            WITH g, count(r) AS reservationCount
            RETURN g.id AS id, g.name AS name, g.email AS email, g.phone AS phone, reservationCount
            ORDER BY g.name
            """;

        var records = await _graph.RunQueryAsync(cypher, new { search });
        return records.Select(MapGuestSummary).ToList();
    }

    public async Task<(GuestSummaryDto? Guest, string? Error)> RegisterGuestAsync(
        RegisterGuestRequest request,
        CancellationToken cancellationToken = default)
    {
        const string duplicateCheck = """
            MATCH (g:Guest)
            WHERE toLower(g.email) = toLower($email)
            RETURN g.id AS id
            LIMIT 1
            """;

        var duplicate = await _graph.RunQuerySingleAsync(duplicateCheck, new { email = request.Email.Trim() });
        if (duplicate is not null)
        {
            return (null, $"A guest with email '{request.Email}' already exists.");
        }

        const string nextIdCypher = """
            OPTIONAL MATCH (g:Guest)
            WHERE g.id STARTS WITH 'G-'
            WITH coalesce(max(toInteger(substring(g.id, 2))), 1000) + 1 AS nextNum
            RETURN 'G-' + toString(nextNum) AS newId
            """;

        var idRecord = await _graph.RunQuerySingleAsync(nextIdCypher);
        var newId = idRecord?["newId"].As<string>() ?? "G-1004";

        const string createCypher = """
            CREATE (g:Guest {
              id: $id,
              name: $name,
              email: $email,
              phone: $phone
            })
            RETURN g.id AS id, g.name AS name, g.email AS email, g.phone AS phone, 0 AS reservationCount
            """;

        var record = await _graph.RunQuerySingleAsync(createCypher, new
        {
            id = newId,
            name = request.Name.Trim(),
            email = request.Email.Trim(),
            phone = request.Phone.Trim()
        });

        return record is null
            ? (null, "Failed to create guest.")
            : (MapGuestSummary(record), null);
    }

    public async Task<GuestDetailDto?> GetGuestByIdAsync(string guestId, CancellationToken cancellationToken = default)
    {
        const string cypherById = """
            MATCH (g:Guest { id: $guestId })
            OPTIONAL MATCH (g)-[:BOOKED]->(r:Reservation)-[:AT_PROPERTY]->(p:Property)
            OPTIONAL MATCH (r)-[:ASSIGNED_TO]->(room:Room)
            RETURN g.id AS id, g.name AS name, g.email AS email, g.phone AS phone,
                   collect(DISTINCT {
                     id: r.id,
                     confirmationNumber: r.confirmationNumber,
                     propertyCode: p.code,
                     propertyName: p.name,
                     roomNumber: room.number,
                     checkIn: r.checkIn,
                     checkOut: r.checkOut,
                     status: r.status
                   }) AS reservations
            """;

        var record = await _graph.RunQuerySingleAsync(cypherById, new { guestId });
        if (record is null)
        {
            // Fallback: allow lookup by exact guest name (case-insensitive)
            const string cypherByName = """
                MATCH (g:Guest)
                WHERE toLower(g.name) = toLower($guestId)
                OPTIONAL MATCH (g)-[:BOOKED]->(r:Reservation)-[:AT_PROPERTY]->(p:Property)
                OPTIONAL MATCH (r)-[:ASSIGNED_TO]->(room:Room)
                RETURN g.id AS id, g.name AS name, g.email AS email, g.phone AS phone,
                       collect(DISTINCT {
                         id: r.id,
                         confirmationNumber: r.confirmationNumber,
                         propertyCode: p.code,
                         propertyName: p.name,
                         roomNumber: room.number,
                         checkIn: r.checkIn,
                         checkOut: r.checkOut,
                         status: r.status
                       }) AS reservations
                LIMIT 1
                """;

            record = await _graph.RunQuerySingleAsync(cypherByName, new { guestId });
        }

        if (record is null)
        {
            return null;
        }

        var reservations = record["reservations"].As<List<Dictionary<string, object?>>>()
            .Where(r => r.ContainsKey("id") && r["id"] is not null)
            .Select(r => new ReservationSummaryDto(
                Id: GetString(r, "id"),
                ConfirmationNumber: GetString(r, "confirmationNumber"),
                PropertyCode: GetString(r, "propertyCode"),
                PropertyName: GetString(r, "propertyName"),
                RoomNumber: GetString(r, "roomNumber"),
                CheckIn: GetString(r, "checkIn"),
                CheckOut: GetString(r, "checkOut"),
                Status: GetString(r, "status")))
            .ToList();

        return new GuestDetailDto(
            Id: record["id"].As<string>(),
            Name: record["name"].As<string>(),
            Email: record["email"].As<string>(),
            Phone: record["phone"].As<string>(),
            Reservations: reservations);
    }

    /// <summary>
    /// Multi-hop traversal: Guest → Reservation → Folio → Charge → TransactionCode → Reversal (3+ hops).
    /// </summary>
    public async Task<RevenueTraceDto?> GetGuestRevenueTraceAsync(string guestId, CancellationToken cancellationToken = default)
    {
        const string cypher = """
            MATCH (g:Guest { id: $guestId })
            OPTIONAL MATCH chargePath = (g)-[:BOOKED]->(r:Reservation)-[:HAS_FOLIO]->(f:Folio)-[:POSTED]->(c:Charge)-[:USES_CODE]->(tc:TransactionCode)
            OPTIONAL MATCH reversalPath = (c)-[:REVERSED_BY*1..2]->(rev:Charge)
            OPTIONAL MATCH payPath = (f)<-[:SETTLES]-(p:Payment)-[:USES_CODE]->(ptc:TransactionCode)
            OPTIONAL MATCH roomPath = (r)-[:ASSIGNED_TO]->(room:Room)
            OPTIONAL MATCH propPath = (r)-[:AT_PROPERTY]->(prop:Property)
            RETURN g.id AS id, g.name AS name, g.email AS email, g.phone AS phone,
                   collect(DISTINCT chargePath) AS chargePaths,
                   collect(DISTINCT reversalPath) AS reversalPaths,
                   collect(DISTINCT payPath) AS payPaths,
                   collect(DISTINCT roomPath) AS roomPaths,
                   collect(DISTINCT propPath) AS propPaths
            """;

        var record = await _graph.RunQuerySingleAsync(cypher, new { guestId });
        if (record is null)
        {
            return null;
        }

        var guest = new GuestSummaryDto(
            Id: record["id"].As<string>(),
            Name: record["name"].As<string>(),
            Email: record["email"].As<string>(),
            Phone: record["phone"].As<string>(),
            ReservationCount: 0);

        var nodes = new Dictionary<string, TraceNodeDto>();
        var edges = new List<TraceEdgeDto>();

        foreach (var pathKey in new[] { "chargePaths", "reversalPaths", "payPaths", "roomPaths", "propPaths" })
        {
            var paths = record[pathKey].As<List<IPath?>>();
            foreach (var path in paths.Where(p => p is not null))
            {
                foreach (var node in path!.Nodes)
                {
                    AddNode(nodes, node);
                }

                foreach (var rel in path.Relationships)
                {
                    edges.Add(new TraceEdgeDto(
                        From: rel.StartNodeElementId,
                        To: rel.EndNodeElementId,
                        Relationship: rel.Type));
                }
            }
        }

        var ledger = await GetReservationLedgerAsync(guestId, cancellationToken);
        var totalCharges = ledger.Where(l => l.Type == "Charge").Sum(l => l.Amount);
        var totalPayments = ledger.Where(l => l.Type == "Payment").Sum(l => l.Amount);

        return new RevenueTraceDto(
            Guest: guest,
            Nodes: nodes.Values.ToList(),
            Edges: edges.DistinctBy(e => $"{e.From}-{e.Relationship}-{e.To}").ToList(),
            TotalCharges: totalCharges,
            TotalPayments: totalPayments,
            Balance: totalCharges - totalPayments);
    }

    /// <summary>
    /// Finds reversal chains — awkward in SQL due to recursive parent-child ledger entries.
    /// </summary>
    public async Task<IReadOnlyList<ReversalChainDto>> GetReversalChainsAsync(CancellationToken cancellationToken = default)
    {
        const string cypher = """
            MATCH (g:Guest)-[:BOOKED]->(r:Reservation)-[:HAS_FOLIO]->(f:Folio)-[:POSTED]->(original:Charge { isReversal: false })
            MATCH path = (original)-[:REVERSED_BY*1..3]->(reversal:Charge)
            WITH g, r, original, path, length(path) AS hops, collect(reversal) AS reversalNodes
            RETURN g.name AS guestName,
                   r.id AS reservationId,
                   original.reference AS originalReference,
                   hops,
                   original { .* , type: 'Charge' } AS originalEntry,
                   [rev IN reversalNodes | rev { .* , type: 'Charge' }] AS reversalEntries
            ORDER BY hops DESC
            """;

        var records = await _graph.RunQueryAsync(cypher);
        return records.Select(r =>
        {
            var entries = new List<LedgerEntryDto>();
            var original = r["originalEntry"].As<Dictionary<string, object>>();
            entries.Add(MapChargeEntry(original, false));

            foreach (var rev in r["reversalEntries"].As<List<Dictionary<string, object>>>())
            {
                entries.Add(MapChargeEntry(rev, true));
            }

            return new ReversalChainDto(
                OriginalReference: r["originalReference"].As<string>(),
                ReservationId: r["reservationId"].As<string>(),
                GuestName: r["guestName"].As<string>(),
                Entries: entries,
                HopCount: r["hops"].As<int>());
        }).ToList();
    }

    /// <summary>
    /// Finds reservations sharing the same posting reference (cross-guest linkage).
    /// </summary>
    public async Task<IReadOnlyList<SearchResultDto>> GetSharedReferenceLinksAsync(string reference, CancellationToken cancellationToken = default)
    {
        const string cypher = """
            MATCH (g:Guest)-[:BOOKED]->(r:Reservation)-[:HAS_FOLIO]->(f:Folio)-[:POSTED]->(c:Charge)
            WHERE c.reference CONTAINS $reference OR c.checkNumber CONTAINS $reference
            RETURN DISTINCT
              'SharedReference' AS matchType,
              g.id AS id,
              g.name AS title,
              r.confirmationNumber + ' · ' + c.reference AS subtitle,
              r.id AS reservationId
            ORDER BY g.name
            """;

        var records = await _graph.RunQueryAsync(cypher, new { reference });
        return records.Select(r => new SearchResultDto(
            MatchType: r["matchType"].As<string>(),
            Id: r["id"].As<string>(),
            Title: r["title"].As<string>(),
            Subtitle: r["subtitle"].As<string>(),
            ReservationId: r["reservationId"].As<string>())).ToList();
    }

    public async Task<IReadOnlyList<LedgerEntryDto>> GetReservationLedgerAsync(string guestId, CancellationToken cancellationToken = default)
    {
        const string cypher = """
            MATCH (g:Guest { id: $guestId })-[:BOOKED]->(:Reservation)-[:HAS_FOLIO]->(f:Folio)
            OPTIONAL MATCH (f)-[:POSTED]->(c:Charge)-[:USES_CODE]->(tc1:TransactionCode)
            OPTIONAL MATCH (f)<-[:SETTLES]-(p:Payment)-[:USES_CODE]->(tc2:TransactionCode)
            WITH collect({
              transactionNo: c.transactionNo,
              type: 'Charge',
              transactionCode: tc1.code,
              amount: c.amount,
              currency: c.currency,
              reference: c.reference,
              isReversal: c.isReversal,
              revenueDate: c.revenueDate
            }) + collect({
              transactionNo: p.transactionNo,
              type: 'Payment',
              transactionCode: tc2.code,
              amount: p.amount,
              currency: p.currency,
              reference: p.reference,
              isReversal: p.isReversal,
              revenueDate: ''
            }) AS entries
            UNWIND entries AS entry
            RETURN entry
            ORDER BY entry.transactionNo
            """;

        var records = await _graph.RunQueryAsync(cypher, new { guestId });
        return records.Select(r =>
        {
            var entry = r["entry"].As<Dictionary<string, object>>();
            return new LedgerEntryDto(
                TransactionNo: GetString(entry, "transactionNo"),
                Type: GetString(entry, "type"),
                TransactionCode: GetString(entry, "transactionCode"),
                Amount: Convert.ToDecimal(entry["amount"]),
                Currency: GetString(entry, "currency"),
                Reference: GetString(entry, "reference"),
                IsReversal: entry.TryGetValue("isReversal", out var rev) && Convert.ToBoolean(rev),
                RevenueDate: GetString(entry, "revenueDate"));
        }).ToList();
    }

    public async Task<IReadOnlyList<RoomListItemDto>> GetRoomsAsync(
        string? propertyCode = null,
        CancellationToken cancellationToken = default)
    {
        const string cypher = """
            MATCH (p:Property)-[:HAS_ROOM]->(room:Room)
            WHERE $propertyCode IS NULL OR $propertyCode = '' OR p.code = $propertyCode
            OPTIONAL MATCH (res:Reservation)-[:ASSIGNED_TO]->(room)
            OPTIONAL MATCH (guest:Guest)-[:BOOKED]->(res)
            WITH p, room, res, guest
            ORDER BY res.status DESC
            WITH p, room, head(collect({
              reservationId: res.id,
              reservationStatus: res.status,
              guestId: guest.id,
              guestName: guest.name
            })) AS assignment
            RETURN p.code AS propertyCode,
                   p.name AS propertyName,
                   room.number AS number,
                   room.name AS name,
                   room.floor AS floor,
                   assignment.reservationId AS reservationId,
                   assignment.reservationStatus AS reservationStatus,
                   assignment.guestId AS guestId,
                   assignment.guestName AS guestName
            ORDER BY room.number
            """;

        var records = await _graph.RunQueryAsync(cypher, new { propertyCode });
        return records.Select(r =>
        {
            var guestId = r["guestId"].As<string?>();
            var reservationStatus = r["reservationStatus"].As<string?>();
            var occupancy = reservationStatus == "InHouse"
                ? "Occupied"
                : !string.IsNullOrEmpty(guestId)
                    ? "Reserved"
                    : "Vacant";

            return new RoomListItemDto(
                Number: r["number"].As<string>(),
                Name: r["name"].As<string>(),
                Floor: r["floor"].As<int>(),
                PropertyCode: r["propertyCode"].As<string>(),
                PropertyName: r["propertyName"].As<string>(),
                GuestId: guestId,
                GuestName: r["guestName"].As<string?>(),
                ReservationId: r["reservationId"].As<string?>(),
                ReservationStatus: reservationStatus,
                OccupancyStatus: occupancy);
        }).ToList();
    }

    public async Task<IReadOnlyList<PropertyStatsDto>> GetPropertyStatsAsync(CancellationToken cancellationToken = default)
    {
        const string cypher = """
            MATCH (p:Property)
            OPTIONAL MATCH (p)-[:HAS_ROOM]->(room:Room)
            OPTIONAL MATCH (active:Reservation { status: 'InHouse' })-[:AT_PROPERTY]->(p)
            OPTIONAL MATCH (res:Reservation)-[:AT_PROPERTY]->(p)
            OPTIONAL MATCH (res)-[:HAS_FOLIO]->(f:Folio { status: 'Open' })
            RETURN p.code AS code, p.name AS name, p.currency AS currency,
                   count(DISTINCT room) AS roomCount,
                   count(DISTINCT active) AS activeReservations,
                   count(DISTINCT f) AS openFolios
            """;

        var records = await _graph.RunQueryAsync(cypher);
        return records.Select(r => new PropertyStatsDto(
            Code: r["code"].As<string>(),
            Name: r["name"].As<string>(),
            Currency: r["currency"].As<string>(),
            RoomCount: r["roomCount"].As<int>(),
            ActiveReservations: r["activeReservations"].As<int>(),
            OpenFolios: r["openFolios"].As<int>())).ToList();
    }

    public async Task<IReadOnlyList<SearchResultDto>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        const string cypher = """
            CALL {
              MATCH (g:Guest)
              WHERE toLower(g.name) CONTAINS toLower($query) OR g.id CONTAINS $query
              RETURN 'Guest' AS matchType, g.id AS id, g.name AS title, g.email AS subtitle, null AS reservationId
              UNION
              MATCH (r:Reservation)
              WHERE r.id CONTAINS $query OR r.confirmationNumber CONTAINS $query
              MATCH (g:Guest)-[:BOOKED]->(r)
              RETURN 'Reservation' AS matchType, r.id AS id, r.confirmationNumber AS title, g.name AS subtitle, r.id AS reservationId
              UNION
              MATCH (c:Charge)
              WHERE c.reference CONTAINS $query OR c.checkNumber CONTAINS $query
              MATCH (f:Folio)-[:POSTED]->(c)<-[:HAS_FOLIO]-(r:Reservation)<-[:BOOKED]-(g:Guest)
              RETURN 'CheckReference' AS matchType, c.transactionNo AS id, c.reference AS title, g.name + ' · Res ' + r.id AS subtitle, r.id AS reservationId
            }
            RETURN matchType, id, title, subtitle, reservationId
            LIMIT 20
            """;

        var records = await _graph.RunQueryAsync(cypher, new { query });
        return records.Select(r => new SearchResultDto(
            MatchType: r["matchType"].As<string>(),
            Id: r["id"].As<string>(),
            Title: r["title"].As<string>(),
            Subtitle: r["subtitle"].As<string>(),
            ReservationId: r["reservationId"].As<string?>())).ToList();
    }

    private static GuestSummaryDto MapGuestSummary(IRecord record) =>
        new(
            Id: record["id"].As<string>(),
            Name: record["name"].As<string>(),
            Email: record["email"].As<string>(),
            Phone: record["phone"].As<string>(),
            ReservationCount: record["reservationCount"].As<int>());

    private static void AddNode(Dictionary<string, TraceNodeDto> nodes, INode node)
    {
        var label = node.Labels.FirstOrDefault() ?? "Node";
        var props = node.Properties.ToDictionary(k => k.Key, v => (object?)v.Value);
        var displayName = label switch
        {
            "Guest" => node.Properties.GetValueOrDefault("name")?.ToString() ?? label,
            "Reservation" => $"Res {node.Properties.GetValueOrDefault("confirmationNumber")}",
            "Folio" => $"Folio {node.Properties.GetValueOrDefault("number")}",
            "Charge" => $"Charge {node.Properties.GetValueOrDefault("transactionNo")}",
            "Payment" => $"Payment {node.Properties.GetValueOrDefault("transactionNo")}",
            "TransactionCode" => node.Properties.GetValueOrDefault("code")?.ToString() ?? label,
            "Property" => node.Properties.GetValueOrDefault("name")?.ToString() ?? label,
            "Room" => $"Room {node.Properties.GetValueOrDefault("number")}",
            _ => label
        };

        nodes[node.ElementId] = new TraceNodeDto(node.ElementId, displayName, label, props);
    }

    private static LedgerEntryDto MapChargeEntry(Dictionary<string, object> entry, bool isReversal) =>
        new(
            TransactionNo: GetString(entry, "transactionNo"),
            Type: GetString(entry, "type"),
            TransactionCode: "",
            Amount: Convert.ToDecimal(entry["amount"]),
            Currency: GetString(entry, "currency"),
            Reference: GetString(entry, "reference"),
            IsReversal: isReversal || (entry.TryGetValue("isReversal", out var rev) && Convert.ToBoolean(rev)),
            RevenueDate: GetString(entry, "revenueDate"));

    private static string GetString(IReadOnlyDictionary<string, object?> dict, string key) =>
        dict.TryGetValue(key, out var value) && value is not null ? value.ToString()! : string.Empty;
}
