using Domain.Entities;
using Domain.Results;
using Xunit;

namespace Api.Tests.Domain;

public class MovementTests
{
    [Fact]
    public void Create_WithValidValues_SetsFields()
    {
        var result = Movement.Create(42, MovementType.Credit, 15000);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value!.AccountId);
        Assert.Equal(MovementType.Credit, result.Value.Type);
        Assert.Equal(15000, result.Value.Amount);
        Assert.NotEqual(default, result.Value.Timestamp);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveAmount_Fails(long amount)
    {
        var result = Movement.Create(42, MovementType.Debit, amount);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.InvalidAmount, result.Error?.Code);
    }
}
