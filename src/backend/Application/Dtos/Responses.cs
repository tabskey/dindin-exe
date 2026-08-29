using Domain.Entities;

namespace Application.Dtos;

public sealed record AccountDto(long Id, string AccountNumber, string Name, string Cpf, AccountType AccountType, DateTime CreatedAt);
public sealed record BalanceDto(long AccountId, decimal Balance);
public sealed record MovementDto(long Id, long AccountId, MovementType Type, decimal Amount, DateTime Timestamp);
public sealed record MovementHistoryDto(IReadOnlyList<MovementDto> Items, int Page, int PageSize, int Total);
