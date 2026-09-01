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

        var hasCounterparty = !string.IsNullOrWhiteSpace(request.CounterpartyCpf)
            || !string.IsNullOrWhiteSpace(request.CounterpartyAccountNumber);

        // Contraparte só existe no depósito (que, com destinatário, vira transferência).
        if (hasCounterparty && request.Type != MovementType.Credit)
        {
            return Result<MovementDto>.Failure(
                new DomainError(DomainErrorCode.InvalidRequest, "Counterparty is only allowed on deposits."));
        }

        Account? target = null;
        if (!string.IsNullOrWhiteSpace(request.CounterpartyAccountNumber))
        {
            target = await _accounts.GetByAccountNumberAsync(request.CounterpartyAccountNumber.Trim(), cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.CounterpartyCpf))
        {
            target = await _accounts.GetByCpfAsync(request.CounterpartyCpf.Trim(), cancellationToken);
        }

        if (hasCounterparty && target is null)
        {
            return Result<MovementDto>.Failure(
                new DomainError(DomainErrorCode.CounterpartyNotFound, "Counterparty account not found."));
        }

        if (target is null)
        {
            return await CreateSelfMovementAsync(account, request, cancellationToken);
        }

        if (target.Id == account.Id)
        {
            return Result<MovementDto>.Failure(
                new DomainError(DomainErrorCode.InvalidRequest, "Cannot transfer to yourself; leave the counterparty empty for a self deposit."));
        }

        // GetByCpf/GetByAccountNumber usam AsNoTracking — recarrega rastreado para
        // alterar o saldo e persistir na mesma transação.
        target = await _accounts.GetByIdAsync(target.Id, cancellationToken);
        return await CreateTransferAsync(account, target!, request, cancellationToken);
    }

    private async Task<Result<MovementDto>> CreateSelfMovementAsync(
        Account account, CreateMovementRequest request, CancellationToken cancellationToken)
    {
        var label = request.Type == MovementType.Credit
            ? CounterpartyLabel.AutoDeposit(account)
            : CounterpartyLabel.AutoWithdrawal(account);

        var movementResult = Movement.Create(account.Id, request.Type, request.Amount, label);
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

        var persist = await PersistWithRetryAsync([(account, strategy, request.Amount)], cancellationToken);
        if (!persist.IsSuccess)
        {
            return Result<MovementDto>.Failure(persist.Error!);
        }

        _logger.LogInformation(
            "Movement created: id={Id}, account={AccountId}, type={Type}, amount={Amount}, counterparty={Counterparty}",
            movement.Id, account.Id, request.Type, request.Amount, label);
        return Result<MovementDto>.Success(ToDto(movement));
    }

    private async Task<Result<MovementDto>> CreateTransferAsync(
        Account account, Account target, CreateMovementRequest request, CancellationToken cancellationToken)
    {
        var debitStrategy = MovementStrategies.For(MovementType.Debit);
        var creditStrategy = MovementStrategies.For(MovementType.Credit);

        var debitMovementResult = Movement.Create(account.Id, MovementType.Debit, request.Amount, CounterpartyLabel.For(target));
        if (!debitMovementResult.IsSuccess)
        {
            return Result<MovementDto>.Failure(debitMovementResult.Error!);
        }

        var creditMovementResult = Movement.Create(target.Id, MovementType.Credit, request.Amount, CounterpartyLabel.For(account));
        if (!creditMovementResult.IsSuccess)
        {
            return Result<MovementDto>.Failure(creditMovementResult.Error!);
        }

        var debitResult = account.ApplyMovement(debitStrategy, request.Amount);
        if (!debitResult.IsSuccess)
        {
            return Result<MovementDto>.Failure(debitResult.Error!);
        }

        var creditResult = target.ApplyMovement(creditStrategy, request.Amount);
        if (!creditResult.IsSuccess)
        {
            return Result<MovementDto>.Failure(creditResult.Error!);
        }

        await _movements.AddAsync(debitMovementResult.Value!, cancellationToken);
        await _movements.AddAsync(creditMovementResult.Value!, cancellationToken);

        var persist = await PersistWithRetryAsync(
            [(account, debitStrategy, request.Amount), (target, creditStrategy, request.Amount)],
            cancellationToken);
        if (!persist.IsSuccess)
        {
            return Result<MovementDto>.Failure(persist.Error!);
        }

        _logger.LogInformation(
            "Transfer created: from={From}, to={To}, amount={Amount}",
            account.Id, target.Id, request.Amount);
        return Result<MovementDto>.Success(ToDto(debitMovementResult.Value!));
    }

    // Persiste com retry de concorrência otimista: em conflito de RowVersion,
    // recarrega TODAS as contas mutadas e reaplica os strategies antes de tentar de
    // novo. A atomicidade é garantida pelo UnitOfWork do filtro de idempotência
    // (rollback se qualquer reaplicação falhar).
    private async Task<Result> PersistWithRetryAsync(
        IReadOnlyList<(Account Account, IMovementStrategy Strategy, long Amount)> mutations,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _movements.SaveChangesAsync(cancellationToken);
                return Result.Success();
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxSaveAttempts)
            {
                foreach (var (account, _, _) in mutations)
                {
                    await _accounts.ReloadAsync(account, cancellationToken);
                }

                foreach (var (account, strategy, amount) in mutations)
                {
                    var result = account.ApplyMovement(strategy, amount);
                    if (!result.IsSuccess)
                    {
                        return Result.Failure(result.Error!);
                    }
                }
            }
        }
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
