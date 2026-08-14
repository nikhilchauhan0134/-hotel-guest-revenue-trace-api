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
        // ==========================================
        // 1. Transaction Codes
        // ==========================================
        MERGE (tc5210:TransactionCode { code: '5210' })
          SET tc5210.name = 'Room Revenue', tc5210.category = 'Revenue'
        MERGE (tc9200:TransactionCode { code: '9200' })
          SET tc9200.name = 'Cash Payment', tc9200.category = 'Payment'
        MERGE (tc3100:TransactionCode { code: '3100' })
          SET tc3100.name = 'Minibar Revenue', tc3100.category = 'Revenue'
        MERGE (tc4100:TransactionCode { code: '4100' })
          SET tc4100.name = 'Spa Revenue', tc4100.category = 'Revenue'
        
        // New Transaction Codes
        MERGE (tc2000:TransactionCode { code: '2000' })
          SET tc2000.name = 'Restaurant Food', tc2000.category = 'Revenue'
        MERGE (tc7000:TransactionCode { code: '7000' })
          SET tc7000.name = 'Laundry Services', tc7000.category = 'Revenue'
        MERGE (tc8000:TransactionCode { code: '8000' })
          SET tc8000.name = 'State Tax (GST)', tc8000.category = 'Tax'
        MERGE (tc9300:TransactionCode { code: '9300' })
          SET tc9300.name = 'Credit Card Payment', tc9300.category = 'Payment'

        // ==========================================
        // 2. Property & Rooms
        // ==========================================
        MERGE (prop:Property { code: '900003' })
          SET prop.name = 'Grand Marina Resort Goa', prop.currency = 'INR'
        
        MERGE (r9200:Room { number: '9200' }) SET r9200.name = 'Suite 9200', r9200.floor = 9
        MERGE (r9201:Room { number: '9201' }) SET r9201.name = 'Deluxe 9201', r9201.floor = 9
        MERGE (r8105:Room { number: '8105' }) SET r8105.name = 'Standard 8105', r8105.floor = 8
        MERGE (r7102:Room { number: '7102' }) SET r7102.name = 'Standard 7102', r7102.floor = 7
        MERGE (r9999:Room { number: '9999' }) SET r9999.name = 'Penthouse 9999', r9999.floor = 10

        MERGE (prop)-[:HAS_ROOM]->(r9200)
        MERGE (prop)-[:HAS_ROOM]->(r9201)
        MERGE (prop)-[:HAS_ROOM]->(r8105)
        MERGE (prop)-[:HAS_ROOM]->(r7102)
        MERGE (prop)-[:HAS_ROOM]->(r9999)

        // ==========================================
        // 3. Guests
        // ==========================================
        MERGE (g1:Guest { id: 'G-1001' })
          SET g1.name = 'Satyam Mishra', g1.email = 'satyam.m@email.in', g1.phone = '+919812345678'
        MERGE (g2:Guest { id: 'G-1002' })
          SET g2.name = 'Sarah Mitchell', g2.email = 'sarah.m@corp.com', g2.phone = '+447911123456'
        MERGE (g3:Guest { id: 'G-1003' })
          SET g3.name = 'Raj Patel', g3.email = 'raj.p@email.com', g3.phone = '+919876543210'
        MERGE (g4:Guest { id: 'G-1004' })
          SET g4.name = 'Priya Sharma', g4.email = 'priya.sharma@email.in', g4.phone = '+919876543211'
        MERGE (g5:Guest { id: 'G-1005' })
          SET g5.name = 'Vikram Singh', g5.email = 'vsingh@corp.in', g5.phone = '+919876543212'

        // ==========================================
        // 4. Reservations & Folios
        // ==========================================

        // --- Reservation 227810 — Reference case with reversal chain ---
        MERGE (res1:Reservation { id: '227810' })
          SET res1.confirmationNumber = '884501', res1.checkIn = '2025-03-15', res1.checkOut = '2025-03-16', res1.status = 'CheckedOut'
        MERGE (g1)-[:BOOKED]->(res1)
        MERGE (res1)-[:AT_PROPERTY]->(prop)
        MERGE (res1)-[:ASSIGNED_TO]->(r9200)

        MERGE (folio1:Folio { number: 'PLE436737' }) SET folio1.windowNo = 1, folio1.status = 'Closed'
        MERGE (res1)-[:HAS_FOLIO]->(folio1)

        MERGE (charge1:Charge { transactionNo: '1029538' })
          SET charge1.amount = 47440.00, charge1.currency = 'INR', charge1.reference = 'CHECK#PLE436737', charge1.checkNumber = 'PLE436737', charge1.revenueDate = '2025-03-15', charge1.isReversal = false
        MERGE (folio1)-[:POSTED]->(charge1)
        MERGE (charge1)-[:USES_CODE]->(tc5210)

        MERGE (pay1:Payment { transactionNo: '1029542' })
          SET pay1.amount = 55980.00, pay1.currency = 'INR', pay1.method = 'PCA', pay1.reference = 'CHECK#PLE436737', pay1.isReversal = false
        MERGE (pay1)-[:SETTLES]->(folio1)
        MERGE (pay1)-[:USES_CODE]->(tc9200)

        MERGE (charge1r:Charge { transactionNo: '1029539' })
          SET charge1r.amount = -47440.00, charge1r.currency = 'INR', charge1r.reference = 'CHECK#PLE436737', charge1r.checkNumber = 'PLE436737', charge1r.revenueDate = '2026-06-09', charge1r.isReversal = true
        MERGE (folio1)-[:POSTED]->(charge1r)
        MERGE (charge1r)-[:USES_CODE]->(tc5210)
        MERGE (charge1)-[:REVERSED_BY]->(charge1r)

        MERGE (pay1r:Payment { transactionNo: '1029543' })
          SET pay1r.amount = -55980.00, pay1r.currency = 'INR', pay1r.method = 'PCA', pay1r.reference = 'CHECK#PLE436737', pay1r.isReversal = true
        MERGE (pay1r)-[:SETTLES]->(folio1)
        MERGE (pay1r)-[:USES_CODE]->(tc9200)
        MERGE (pay1)-[:REVERSED_BY]->(pay1r)

        MERGE (charge1b:Charge { transactionNo: '1029540' })
          SET charge1b.amount = 47440.00, charge1b.currency = 'INR', charge1b.reference = 'CHECK#PLE436737-R', charge1b.checkNumber = 'PLE436737', charge1b.revenueDate = '2026-06-09', charge1b.isReversal = false
        MERGE (folio1)-[:POSTED]->(charge1b)
        MERGE (charge1b)-[:USES_CODE]->(tc5210)

        MERGE (pay1b:Payment { transactionNo: '1029544' })
          SET pay1b.amount = 55980.00, pay1b.currency = 'INR', pay1b.method = 'PCA', pay1b.reference = 'CHECK#PLE436737-R', pay1b.isReversal = false
        MERGE (pay1b)-[:SETTLES]->(folio1)
        MERGE (pay1b)-[:USES_CODE]->(tc9200)

        // --- Reservation 228901 — Sarah Mitchell, multi-charge folio ---
        MERGE (res2:Reservation { id: '228901' })
          SET res2.confirmationNumber = '885612', res2.checkIn = '2025-04-02', res2.checkOut = '2025-04-05', res2.status = 'InHouse'
        MERGE (g2)-[:BOOKED]->(res2)
        MERGE (res2)-[:AT_PROPERTY]->(prop)
        MERGE (res2)-[:ASSIGNED_TO]->(r9201)

        MERGE (folio2:Folio { number: 'FOL-885612-A' }) SET folio2.windowNo = 1, folio2.status = 'Open'
        MERGE (res2)-[:HAS_FOLIO]->(folio2)

        MERGE (c2a:Charge { transactionNo: '1031001' })
          SET c2a.amount = 12000.0, c2a.currency = 'INR', c2a.reference = 'ROOM#885612', c2a.revenueDate = '2025-04-02', c2a.isReversal = false
        MERGE (folio2)-[:POSTED]->(c2a)
        MERGE (c2a)-[:USES_CODE]->(tc5210)

        MERGE (c2b:Charge { transactionNo: '1031002' })
          SET c2b.amount = 3500.0, c2b.currency = 'INR', c2b.reference = 'MINI#885612', c2b.revenueDate = '2025-04-03', c2b.isReversal = false
        MERGE (folio2)-[:POSTED]->(c2b)
        MERGE (c2b)-[:USES_CODE]->(tc3100)

        MERGE (c2c:Charge { transactionNo: '1031003' })
          SET c2c.amount = 8900.0, c2c.currency = 'INR', c2c.reference = 'SPA#885612', c2c.revenueDate = '2025-04-04', c2c.isReversal = false
        MERGE (folio2)-[:POSTED]->(c2c)
        MERGE (c2c)-[:USES_CODE]->(tc4100)

        MERGE (p2a:Payment { transactionNo: '1031004' })
          SET p2a.amount = 12000.0, p2a.currency = 'INR', p2a.method = 'CC', p2a.reference = 'DEP#885612', p2a.isReversal = false
        MERGE (p2a)-[:SETTLES]->(folio2)
        MERGE (p2a)-[:USES_CODE]->(tc9300)

        // --- Reservation 229045 — Raj Patel, corporate link ---
        MERGE (res3:Reservation { id: '229045' })
          SET res3.confirmationNumber = '886201', res3.checkIn = '2025-03-14', res3.checkOut = '2025-03-17', res3.status = 'CheckedOut'
        MERGE (g3)-[:BOOKED]->(res3)
        MERGE (res3)-[:AT_PROPERTY]->(prop)
        MERGE (res3)-[:ASSIGNED_TO]->(r8105)

        MERGE (folio3:Folio { number: 'FOL-886201-A' }) SET folio3.windowNo = 1, folio3.status = 'Closed'
        MERGE (res3)-[:HAS_FOLIO]->(folio3)

        MERGE (c3:Charge { transactionNo: '1032001' })
          SET c3.amount = 21000.0, c3.currency = 'INR', c3.reference = 'CHECK#PLE436737', c3.checkNumber = 'PLE436737', c3.revenueDate = '2025-03-15', c3.isReversal = false
        MERGE (folio3)-[:POSTED]->(c3)
        MERGE (c3)-[:USES_CODE]->(tc5210)

        MERGE (p3:Payment { transactionNo: '1032002' })
          SET p3.amount = 21000.0, p3.currency = 'INR', p3.method = 'PCA', p3.reference = 'CHECK#PLE436737', p3.isReversal = false
        MERGE (p3)-[:SETTLES]->(folio3)
        MERGE (p3)-[:USES_CODE]->(tc9200)

        // --- Reservation 230101 — Priya Sharma, Split Payment ---
        MERGE (res4:Reservation { id: '230101' })
          SET res4.confirmationNumber = '891005', res4.checkIn = '2025-05-10', res4.checkOut = '2025-05-12', res4.status = 'CheckedOut'
        MERGE (g4)-[:BOOKED]->(res4)
        MERGE (res4)-[:AT_PROPERTY]->(prop)
        MERGE (res4)-[:ASSIGNED_TO]->(r7102)

        MERGE (folio4:Folio { number: 'FOL-891005-A' }) SET folio4.windowNo = 1, folio4.status = 'Closed'
        MERGE (res4)-[:HAS_FOLIO]->(folio4)

        MERGE (c4a:Charge { transactionNo: '1041001' })
          SET c4a.amount = 18000.0, c4a.currency = 'INR', c4a.reference = 'ROOM#891005', c4a.revenueDate = '2025-05-10', c4a.isReversal = false
        MERGE (folio4)-[:POSTED]->(c4a)
        MERGE (c4a)-[:USES_CODE]->(tc5210)
        
        MERGE (c4b:Charge { transactionNo: '1041002' })
          SET c4b.amount = 3240.0, c4b.currency = 'INR', c4b.reference = 'GST@18%', c4b.revenueDate = '2025-05-10', c4b.isReversal = false
        MERGE (folio4)-[:POSTED]->(c4b)
        MERGE (c4b)-[:USES_CODE]->(tc8000)

        // Split Payment: 10,000 INR Cash, 11,240 INR Credit Card
        MERGE (p4a:Payment { transactionNo: '1041003' })
          SET p4a.amount = 10000.0, p4a.currency = 'INR', p4a.method = 'CASH', p4a.reference = 'SPLIT#1', p4a.isReversal = false
        MERGE (p4a)-[:SETTLES]->(folio4)
        MERGE (p4a)-[:USES_CODE]->(tc9200)
        
        MERGE (p4b:Payment { transactionNo: '1041004' })
          SET p4b.amount = 11240.0, p4b.currency = 'INR', p4b.method = 'CC', p4b.reference = 'SPLIT#2', p4b.isReversal = false
        MERGE (p4b)-[:SETTLES]->(folio4)
        MERGE (p4b)-[:USES_CODE]->(tc9300)

        // --- Reservation 230205 — Vikram Singh, Penthouse with F&B & Laundry ---
        MERGE (res5:Reservation { id: '230205' })
          SET res5.confirmationNumber = '892250', res5.checkIn = '2025-06-01', res5.checkOut = '2025-06-04', res5.status = 'InHouse'
        MERGE (g5)-[:BOOKED]->(res5)
        MERGE (res5)-[:AT_PROPERTY]->(prop)
        MERGE (res5)-[:ASSIGNED_TO]->(r9999)

        MERGE (folio5:Folio { number: 'FOL-892250-A' }) SET folio5.windowNo = 1, folio5.status = 'Open'
        MERGE (res5)-[:HAS_FOLIO]->(folio5)

        MERGE (c5a:Charge { transactionNo: '1052001' })
          SET c5a.amount = 45000.0, c5a.currency = 'INR', c5a.reference = 'PENTHOUSE', c5a.revenueDate = '2025-06-01', c5a.isReversal = false
        MERGE (folio5)-[:POSTED]->(c5a)
        MERGE (c5a)-[:USES_CODE]->(tc5210)

        MERGE (c5b:Charge { transactionNo: '1052002' })
          SET c5b.amount = 8500.0, c5b.currency = 'INR', c5b.reference = 'DINNER#9999', c5b.revenueDate = '2025-06-02', c5b.isReversal = false
        MERGE (folio5)-[:POSTED]->(c5b)
        MERGE (c5b)-[:USES_CODE]->(tc2000)
        
        MERGE (c5c:Charge { transactionNo: '1052003' })
          SET c5c.amount = 1200.0, c5c.currency = 'INR', c5c.reference = 'DRYCLEAN#9999', c5c.revenueDate = '2025-06-02', c5c.isReversal = false
        MERGE (folio5)-[:POSTED]->(c5c)
        MERGE (c5c)-[:USES_CODE]->(tc7000)
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
