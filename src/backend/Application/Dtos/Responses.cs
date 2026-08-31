using Domain.Entities;

namespace Application.Dtos;

public sealed record AccountDto(long Id, string AccountNumber, string Name, string Cpf, AccountType AccountType, DateTime CreatedAt);
public sealed record BalanceDto(long AccountId, long Balance);
public sealed record MovementDto(long Id, long AccountId, MovementType Type, long Amount, DateTime Timestamp, string? Counterparty);
public sealed record MovementHistoryDto(IReadOnlyList<MovementDto> Items, int Page, int PageSize, int Total);
public sealed record AvatarDto(byte[] Data, string ContentType);
