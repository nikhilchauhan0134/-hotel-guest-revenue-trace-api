namespace HotelGraphApi.Models.Dtos;

public record HealthResponse(bool DatabaseConnected, string Message);

public record GuestSummaryDto(
    string Id,
    string Name,
    string Email,
    string Phone,
    int ReservationCount);

public record GuestDetailDto(
    string Id,
    string Name,
    string Email,
    string Phone,
    IReadOnlyList<ReservationSummaryDto> Reservations);

public record ReservationSummaryDto(
    string Id,
    string ConfirmationNumber,
    string PropertyCode,
    string PropertyName,
    string RoomNumber,
    string CheckIn,
    string CheckOut,
    string Status);

public record TraceNodeDto(
    string Id,
    string Label,
    string Type,
    Dictionary<string, object?> Properties);

public record TraceEdgeDto(
    string From,
    string To,
    string Relationship);

public record RevenueTraceDto(
    GuestSummaryDto Guest,
    IReadOnlyList<TraceNodeDto> Nodes,
    IReadOnlyList<TraceEdgeDto> Edges,
    decimal TotalCharges,
    decimal TotalPayments,
    decimal Balance);

public record ReversalChainDto(
    string OriginalReference,
    string ReservationId,
    string GuestName,
    IReadOnlyList<LedgerEntryDto> Entries,
    int HopCount);

public record LedgerEntryDto(
    string TransactionNo,
    string Type,
    string TransactionCode,
    decimal Amount,
    string Currency,
    string Reference,
    bool IsReversal,
    string RevenueDate);

public record PropertyStatsDto(
    string Code,
    string Name,
    string Currency,
    int RoomCount,
    int ActiveReservations,
    int OpenFolios);

public record SeedResultDto(
    bool Success,
    string Message,
    int NodesCreated,
    int RelationshipsCreated);

public record SearchResultDto(
    string MatchType,
    string Id,
    string Title,
    string Subtitle,
    string? ReservationId);

public record RegisterGuestRequest(
    string Name,
    string Email,
    string Phone);

public record RoomListItemDto(
    string Number,
    string Name,
    int Floor,
    string PropertyCode,
    string PropertyName,
    string? GuestId,
    string? GuestName,
    string? ReservationId,
    string? ReservationStatus,
    string OccupancyStatus);
