using Application.Abstractions;
using Application.Dtos;
using Domain.Entities;
using Domain.Movements;
using Domain.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public sealed class MovementService : IMovementService
{
    private const int MaxSaveAttempts = 3;
    private readonly IAccountRepository _accounts;
    private readonly IMovementRepository _movements;
    private readonly ILogger<MovementService> _logger;

    public MovementService(IAccountRepository accounts, IMovementRepository movements, ILogger<MovementService> logger)
    {
        _accounts = accounts;
        _movements = movements;
        _logger = logger;
    }

    public async Task<Result<MovementDto>> CreateAsync(long accountId, CreateMovementRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(typeof(MovementType), request.Type))
        {
            return Result<MovementDto>.Failure(
                new DomainError(DomainErrorCode.InvalidRequest, "Invalid movement type."));
        }

        var account = await _accounts.GetByIdAsync(accountId, cancellationToken);
        if (account is null)
        {
            return Result<MovementDto>.Failure(
                new DomainError(DomainErrorCode.AccountNotFound, "Account not found."));
        }

        string counterparty;
        if (!string.IsNullOrWhiteSpace(request.CounterpartyAccountNumber))
        {
            var byAccountNumber = await _accounts.GetByAccountNumberAsync(request.CounterpartyAccountNumber.Trim(), cancellationToken);
            if (byAccountNumber is null)
            {
                return Result<MovementDto>.Failure(
                    new DomainError(DomainErrorCode.CounterpartyNotFound, "Counterparty account not found."));
            }

            counterparty = CounterpartyLabel.For(byAccountNumber);
        }
        else if (string.IsNullOrWhiteSpace(request.CounterpartyCpf))
        {
            // Sem contraparte: auto-depósito (crédito) ou auto-saque (débito) — o próprio titular.
            counterparty = request.Type == MovementType.Credit
                ? CounterpartyLabel.AutoDeposit(account)
                : CounterpartyLabel.AutoWithdrawal(account);
        }
        else
        {
            var counterpartyAccount = await _accounts.GetByCpfAsync(request.CounterpartyCpf.Trim(), cancellationToken);
            if (counterpartyAccount is null)
            {
                return Result<MovementDto>.Failure(
                    new DomainError(DomainErrorCode.CounterpartyNotFound, "Counterparty account not found."));
            }

            counterparty = CounterpartyLabel.For(counterpartyAccount);
        }

        var movementResult = Movement.Create(accountId, request.Type, request.Amount, counterparty);
        if (!movementResult.IsSuccess)
        {
            return Result<MovementDto>.Failure(movementResult.Error!);
        }

        var strategy = MovementStrategies.For(request.Type);
        var applyResult = account.ApplyMovement(strategy, request.Amount);
        if (!applyResult.IsSuccess)
        {
            return Result<MovementDto>.Failure(applyResult.Error!);
        }

        var movement = movementResult.Value!;
        await _movements.AddAsync(movement, cancellationToken);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _movements.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxSaveAttempts)
            {
                await _accounts.ReloadAsync(account, cancellationToken);
                var retryResult = account.ApplyMovement(strategy, request.Amount);
                if (!retryResult.IsSuccess)
                {
                    return Result<MovementDto>.Failure(retryResult.Error!);
                }
            }
        }

        _logger.LogInformation(
            "Movement created: id={Id}, account={AccountId}, type={Type}, amount={Amount}, counterparty={Counterparty}",
            movement.Id, accountId, request.Type, request.Amount, counterparty);
        return Result<MovementDto>.Success(ToDto(movement));
    }

    public async Task<Result<MovementHistoryDto>> GetHistoryAsync(long accountId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetByIdAsync(accountId, cancellationToken);
        if (account is null)
        {
            return Result<MovementHistoryDto>.Failure(
                new DomainError(DomainErrorCode.AccountNotFound, "Account not found."));
        }

        var (items, total) = await _movements.GetByAccountIdAsync(accountId, page, pageSize, cancellationToken);
        return Result<MovementHistoryDto>.Success(
            new MovementHistoryDto(items.Select(ToDto).ToList(), page, pageSize, total));
    }

    private static MovementDto ToDto(Movement movement) =>
        new(movement.Id, movement.AccountId, movement.Type, movement.Amount, movement.Timestamp, movement.Counterparty);
}
