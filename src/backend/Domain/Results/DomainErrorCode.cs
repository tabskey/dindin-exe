namespace Domain.Results;

public enum DomainErrorCode
{
    InvalidRequest,
    InsufficientBalance,
    InvalidAmount,
    WeakPassword,
    InvalidAvatar,
    AvatarNotFound,
    AccountNumberCollision,
    CpfAlreadyRegistered,
    AccountNotFound,
    InvalidCredentials,
    CounterpartyNotFound
}
