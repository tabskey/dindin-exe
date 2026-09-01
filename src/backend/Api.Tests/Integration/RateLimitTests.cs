using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Api.Tests.Integration;

// Rate limiter "sensitive-write" (30 req/min, fixed window) sobre POST /accounts e
// /auth/login. Factory própria: o limite é por instância do servidor — esgotar a
// janela aqui não pode contaminar os outros testes.
public class RateLimitTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public RateLimitTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_BeyondPermitPerWindow_Returns429()
    {
        // 30 permitidos por janela; a 31ª requisição na mesma janela é rejeitada.
        for (var i = 0; i < 30; i++)
        {
            var allowed = await _factory.Client.PostAsJsonAsync(
                "/auth/login", new { cpf = "111.111.111-11", password = "senha123" });
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        var rejected = await _factory.Client.PostAsJsonAsync(
            "/auth/login", new { cpf = "111.111.111-11", password = "senha123" });
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }
}
