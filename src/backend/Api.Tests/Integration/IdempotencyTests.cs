using System.Net;
using System.Net.Http.Json;
using Application.Dtos;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration;

public class IdempotencyTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public IdempotencyTests(ApiFactory factory) => _factory = factory;

    // Replay com a mesma chave mas request diferente deve ser rejeitado (409), não
    // devolver a resposta cacheada da operação original.
    [Fact]
    public async Task Replay_SameKeyDifferentBody_ReturnsConflict()
    {
        var (id, _, token) = await _factory.RegisterAsync("Idempotência");

        var first = await _factory.PostAsync($"/accounts/{id}/movements",
            new { type = 0, amount = 10 }, token, "same-key-diff-body");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var replay = await _factory.PostAsync($"/accounts/{id}/movements",
            new { type = 0, amount = 20 }, token, "same-key-diff-body");

        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
    }

    // Campo obrigatório nulo deve virar 400 (erro de negócio), nunca 500.
    [Fact]
    public async Task CreateAccount_NullCpf_ReturnsBadRequest()
    {
        var response = await _factory.Client.PostAsJsonAsync("/accounts",
            new { name = "Sem CPF", cpf = (string?)null, accountType = 0, password = "senha123" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_NullCpf_ReturnsUnauthorized()
    {
        var response = await _factory.Client.PostAsJsonAsync("/auth/login",
            new { cpf = (string?)null, password = "senha123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // A auditoria da movimentação deve persistir a contraparte resolvida.
    [Fact]
    public async Task MovementAudit_RecordsCounterparty()
    {
        var (id, _, token) = await _factory.RegisterAsync("Auditada");

        var created = await _factory.PostAsync($"/accounts/{id}/movements",
            new { type = 0, amount = 25, counterpartyCpf = "111.111.111-11" }, token, "audit-counterparty");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var movement = await created.Content.ReadFromJsonAsync<MovementDto>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.EntityType == "Movement" && a.EntityId == movement!.Id.ToString());

        Assert.NotNull(audit);
        Assert.Equal("create", audit!.Action);
        Assert.Contains(movement!.Counterparty!, audit.Payload);
    }
}
