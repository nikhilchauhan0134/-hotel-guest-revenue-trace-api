using HotelGraphApi.Models.Dtos;
using Neo4j.Driver;

namespace HotelGraphApi.Services;

public class SeedService
{
    private readonly IGraphDatabaseService _graph;

    public SeedService(IGraphDatabaseService graph)
    {
        _graph = graph;
    }

    public async Task<SeedResultDto> SeedAsync(CancellationToken cancellationToken = default)
    {
        await ClearGraphAsync();

        const string seedCypher = """
            // Transaction codes
            MERGE (tc5210:TransactionCode { code: '5210' })
              SET tc5210.name = 'Room Service Revenue', tc5210.category = 'Revenue'
            MERGE (tc9200:TransactionCode { code: '9200' })
              SET tc9200.name = 'Cash Payment', tc9200.category = 'Payment'
            MERGE (tc3100:TransactionCode { code: '3100' })
              SET tc3100.name = 'Minibar Revenue', tc3100.category = 'Revenue'
            MERGE (tc4100:TransactionCode { code: '4100' })
              SET tc4100.name = 'Spa Revenue', tc4100.category = 'Revenue'

            // Property & rooms (OHIP PropertyCode 900003)
            MERGE (prop:Property { code: '900003' })
              SET prop.name = 'Grand Marina Resort', prop.currency = 'AED'
            MERGE (r9200:Room { number: '9200' })
              SET r9200.name = 'Suite 9200', r9200.floor = 9
            MERGE (r9201:Room { number: '9201' })
              SET r9201.name = 'Deluxe 9201', r9201.floor = 9
            MERGE (r8105:Room { number: '8105' })
              SET r8105.name = 'Standard 8105', r8105.floor = 8
            MERGE (prop)-[:HAS_ROOM]->(r9200)
            MERGE (prop)-[:HAS_ROOM]->(r9201)
            MERGE (prop)-[:HAS_ROOM]->(r8105)

            // Guests
            MERGE (g1:Guest { id: 'G-1001' })
              SET g1.name = 'Ahmed Al Mansoori', g1.email = 'ahmed.m@email.com', g1.phone = '+971501234567'
            MERGE (g2:Guest { id: 'G-1002' })
              SET g2.name = 'Sarah Mitchell', g2.email = 'sarah.m@corp.com', g2.phone = '+971509876543'
            MERGE (g3:Guest { id: 'G-1003' })
              SET g3.name = 'Raj Patel', g3.email = 'raj.p@email.com', g3.phone = '+971507654321'

            // Reservation 227810 — OHIP reference case with reversal chain
            MERGE (res1:Reservation { id: '227810' })
              SET res1.confirmationNumber = '884501',
                  res1.checkIn = '2025-03-15',
                  res1.checkOut = '2025-03-16',
                  res1.status = 'CheckedOut'
            MERGE (g1)-[:BOOKED]->(res1)
            MERGE (res1)-[:AT_PROPERTY]->(prop)
            MERGE (res1)-[:ASSIGNED_TO]->(r9200)

            MERGE (folio1:Folio { number: 'PLE436737' })
              SET folio1.windowNo = 1, folio1.status = 'Closed'
            MERGE (res1)-[:HAS_FOLIO]->(folio1)

            MERGE (charge1:Charge { transactionNo: '1029538' })
              SET charge1.amount = 4744.08, charge1.currency = 'AED',
                  charge1.reference = 'CHECK#PLE436737', charge1.checkNumber = 'PLE436737',
                  charge1.revenueDate = '2025-03-15', charge1.isReversal = false
            MERGE (folio1)-[:POSTED]->(charge1)
            MERGE (charge1)-[:USES_CODE]->(tc5210)

            MERGE (pay1:Payment { transactionNo: '1029542' })
              SET pay1.amount = 5598.0, pay1.currency = 'AED', pay1.method = 'PCA',
                  pay1.reference = 'CHECK#PLE436737', pay1.isReversal = false
            MERGE (pay1)-[:SETTLES]->(folio1)
            MERGE (pay1)-[:USES_CODE]->(tc9200)

            MERGE (charge1r:Charge { transactionNo: '1029539' })
              SET charge1r.amount = -4744.08, charge1r.currency = 'AED',
                  charge1r.reference = 'CHECK#PLE436737', charge1r.checkNumber = 'PLE436737',
                  charge1r.revenueDate = '2026-06-09', charge1r.isReversal = true
            MERGE (folio1)-[:POSTED]->(charge1r)
            MERGE (charge1r)-[:USES_CODE]->(tc5210)
            MERGE (charge1)-[:REVERSED_BY]->(charge1r)

            MERGE (pay1r:Payment { transactionNo: '1029543' })
              SET pay1r.amount = -5598.0, pay1r.currency = 'AED', pay1r.method = 'PCA',
                  pay1r.reference = 'CHECK#PLE436737', pay1r.isReversal = true
            MERGE (pay1r)-[:SETTLES]->(folio1)
            MERGE (pay1r)-[:USES_CODE]->(tc9200)
            MERGE (pay1)-[:REVERSED_BY]->(pay1r)

            MERGE (charge1b:Charge { transactionNo: '1029540' })
              SET charge1b.amount = 4744.08, charge1b.currency = 'AED',
                  charge1b.reference = 'CHECK#PLE436737-R', charge1b.checkNumber = 'PLE436737',
                  charge1b.revenueDate = '2026-06-09', charge1b.isReversal = false
            MERGE (folio1)-[:POSTED]->(charge1b)
            MERGE (charge1b)-[:USES_CODE]->(tc5210)

            MERGE (pay1b:Payment { transactionNo: '1029544' })
              SET pay1b.amount = 5598.0, pay1b.currency = 'AED', pay1b.method = 'PCA',
                  pay1b.reference = 'CHECK#PLE436737-R', pay1b.isReversal = false
            MERGE (pay1b)-[:SETTLES]->(folio1)
            MERGE (pay1b)-[:USES_CODE]->(tc9200)

            // Reservation 228901 — Sarah Mitchell, multi-charge folio
            MERGE (res2:Reservation { id: '228901' })
              SET res2.confirmationNumber = '885612',
                  res2.checkIn = '2025-04-02',
                  res2.checkOut = '2025-04-05',
                  res2.status = 'InHouse'
            MERGE (g2)-[:BOOKED]->(res2)
            MERGE (res2)-[:AT_PROPERTY]->(prop)
            MERGE (res2)-[:ASSIGNED_TO]->(r9201)

            MERGE (folio2:Folio { number: 'FOL-885612-A' })
              SET folio2.windowNo = 1, folio2.status = 'Open'
            MERGE (res2)-[:HAS_FOLIO]->(folio2)

            MERGE (c2a:Charge { transactionNo: '1031001' })
              SET c2a.amount = 1200.0, c2a.currency = 'AED', c2a.reference = 'ROOM#885612',
                  c2a.checkNumber = '', c2a.revenueDate = '2025-04-02', c2a.isReversal = false
            MERGE (folio2)-[:POSTED]->(c2a)
            MERGE (c2a)-[:USES_CODE]->(tc5210)

            MERGE (c2b:Charge { transactionNo: '1031002' })
              SET c2b.amount = 350.0, c2b.currency = 'AED', c2b.reference = 'MINI#885612',
                  c2b.checkNumber = '', c2b.revenueDate = '2025-04-03', c2b.isReversal = false
            MERGE (folio2)-[:POSTED]->(c2b)
            MERGE (c2b)-[:USES_CODE]->(tc3100)

            MERGE (c2c:Charge { transactionNo: '1031003' })
              SET c2c.amount = 890.0, c2c.currency = 'AED', c2c.reference = 'SPA#885612',
                  c2c.checkNumber = '', c2c.revenueDate = '2025-04-04', c2c.isReversal = false
            MERGE (folio2)-[:POSTED]->(c2c)
            MERGE (c2c)-[:USES_CODE]->(tc4100)

            MERGE (p2a:Payment { transactionNo: '1031004' })
              SET p2a.amount = 1200.0, p2a.currency = 'AED', p2a.method = 'CC',
                  p2a.reference = 'DEP#885612', p2a.isReversal = false
            MERGE (p2a)-[:SETTLES]->(folio2)
            MERGE (p2a)-[:USES_CODE]->(tc9200)

            // Reservation 229045 — Raj Patel, shared check reference with res1 (corporate link)
            MERGE (res3:Reservation { id: '229045' })
              SET res3.confirmationNumber = '886201',
                  res3.checkIn = '2025-03-14',
                  res3.checkOut = '2025-03-17',
                  res3.status = 'CheckedOut'
            MERGE (g3)-[:BOOKED]->(res3)
            MERGE (res3)-[:AT_PROPERTY]->(prop)
            MERGE (res3)-[:ASSIGNED_TO]->(r8105)

            MERGE (folio3:Folio { number: 'FOL-886201-A' })
              SET folio3.windowNo = 1, folio3.status = 'Closed'
            MERGE (res3)-[:HAS_FOLIO]->(folio3)

            MERGE (c3:Charge { transactionNo: '1032001' })
              SET c3.amount = 2100.0, c3.currency = 'AED', c3.reference = 'CHECK#PLE436737',
                  c3.checkNumber = 'PLE436737', c3.revenueDate = '2025-03-15', c3.isReversal = false
            MERGE (folio3)-[:POSTED]->(c3)
            MERGE (c3)-[:USES_CODE]->(tc5210)

            MERGE (p3:Payment { transactionNo: '1032002' })
              SET p3.amount = 2100.0, p3.currency = 'AED', p3.method = 'PCA',
                  p3.reference = 'CHECK#PLE436737', p3.isReversal = false
            MERGE (p3)-[:SETTLES]->(folio3)
            MERGE (p3)-[:USES_CODE]->(tc9200)
            """;

        await _graph.ExecuteWriteAsync(seedCypher);

        var stats = await _graph.RunQuerySingleAsync("""
            MATCH (n)
            WITH count(n) AS nodes
            MATCH ()-[r]->()
            RETURN nodes, count(r) AS rels
            """);

        return new SeedResultDto(
            Success: true,
            Message: "Seed data loaded successfully (OHIP-inspired revenue trace dataset).",
            NodesCreated: stats?["nodes"].As<int>() ?? 0,
            RelationshipsCreated: stats?["rels"].As<int>() ?? 0);
    }

    private async Task ClearGraphAsync()
    {
        await _graph.ExecuteWriteAsync("MATCH (n) DETACH DELETE n");
    }
}
