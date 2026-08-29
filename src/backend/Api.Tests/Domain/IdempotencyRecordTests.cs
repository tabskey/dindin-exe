using Domain.Entities;
using Xunit;

namespace Api.Tests.Domain;

public class IdempotencyRecordTests
{
    [Fact]
    public void Create_SetsFields()
    {
        var record = IdempotencyRecord.Create("key-1", "/accounts", "hash", 201, "{}");

        Assert.Equal("key-1", record.Key);
        Assert.Equal("/accounts", record.RequestPath);
        Assert.Equal("hash", record.RequestHash);
        Assert.Equal(201, record.ResponseStatusCode);
        Assert.Equal("{}", record.ResponseBody);
        Assert.NotEqual(default, record.CreatedAt);
    }
}
