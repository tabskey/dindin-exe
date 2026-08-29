using Domain.Entities;

namespace Application.Dtos;

public sealed record CreateAccountRequest(string Name, string Cpf, AccountType AccountType, string Password);
public sealed record LoginRequest(string Cpf, string Password);
public sealed record CreateMovementRequest(MovementType Type, decimal Amount, string? CounterpartyCpf = null);
