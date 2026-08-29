using Domain.Entities;
using Domain.Results;
using Xunit;

namespace Api.Tests.Domain;

public class MovementTests
{
    [Fact]
    public void Create_WithValidValues_SetsFields()
    {
        var result = Movement.Create(42, MovementType.Credit, 150.00m);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value!.AccountId);
        Assert.Equal(MovementType.Credit, result.Value.Type);
        Assert.Equal(150.00m, result.Value.Amount);
        Assert.NotEqual(default, result.Value.Timestamp);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveAmount_Fails(decimal amount)
    {
        var result = Movement.Create(42, MovementType.Debit, amount);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.InvalidAmount, result.Error?.Code);
    }
}
