using Domain.Entities;
using Xunit;

namespace Api.Tests.Domain;

public class AuditLogTests
{
    [Fact]
    public void Create_SetsFields()
    {
        var log = AuditLog.Create("Account", "1", "create", "{}");

        Assert.Equal("Account", log.EntityType);
        Assert.Equal("1", log.EntityId);
        Assert.Equal("create", log.Action);
        Assert.Equal("{}", log.Payload);
        Assert.NotEqual(default, log.Timestamp);
    }
}
