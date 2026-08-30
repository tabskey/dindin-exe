using System.Data.Common;
using System.Net.Http.Json;
using Application.Dtos;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Tests.Integration;

// Fábrica que sobe a API real (WebApplicationFactory<Program>) sobre um SQLite em
// arquivo temporário, migrado e semeado pela própria inicialização do app.
// Arquivo em vez de :memory:: o banco em memória compartilha uma única conexão,
// o que não suporta requisições concorrentes (teste de débitos paralelos).
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"dindin-test-{Guid.NewGuid():N}.db");
    private HttpClient? _client;

    public HttpClient Client => _client ??= CreateClient();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = $"Data Source={_dbPath};Pooling=False";
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(connectionString)
                    .AddInterceptors(new RowVersionInterceptor(), new BusyTimeoutInterceptor()));
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        foreach (var path in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    public async Task<(long Id, string Cpf, string Token)> RegisterAsync(string name = "Conta de Teste")
    {
        var cpf = NewCpf();
        var create = await Client.PostAsJsonAsync("/accounts", new { name, cpf, accountType = 0, password = "senha123" });
        create.EnsureSuccessStatusCode();
        var account = await create.Content.ReadFromJsonAsync<AccountDto>();
        var token = await LoginAsync(cpf);
        return (account!.Id, cpf, token);
    }

    public async Task<string> LoginAsync(string cpf, string password = "senha123")
    {
        var response = await Client.PostAsJsonAsync("/auth/login", new { cpf, password });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return login!.Token;
    }

    public async Task<HttpResponseMessage> PostAsync(string url, object body, string token, string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("Authorization", $"Bearer {token}");
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return await Client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> GetAsync(string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", $"Bearer {token}");
        return await Client.SendAsync(request);
    }

    // CPF único por chamada; o backend não valida formato, apenas unicidade.
    public static string NewCpf()
    {
        var digits = Guid.NewGuid().ToString("N")[..11];
        return $"{digits[..3]}.{digits[3..6]}.{digits[6..9]}-{digits[9..]}";
    }

    public static string MaskCpf(string cpf)
    {
        var digits = new string(cpf.Where(char.IsAsciiDigit).ToArray());
        return $"{digits[^5..^2]}-{digits[^2..]}";
    }

    // SQLite por padrão falha imediatamente (SQLITE_BUSY) quando duas conexões escrevem ao
    // mesmo tempo; o timeout faz a segunda esperar o lock e seguir o fluxo normal de retry
    // do RowVersion, em vez de estourar erro de banco.
    private sealed class BusyTimeoutInterceptor : DbConnectionInterceptor
    {
        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            SetBusyTimeout(connection);
            base.ConnectionOpened(connection, eventData);
        }

        public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            SetBusyTimeout(connection);
            return base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        }

        private static void SetBusyTimeout(DbConnection connection)
        {
            if (connection is not SqliteConnection sqlite)
            {
                return;
            }

            using var command = sqlite.CreateCommand();
            command.CommandText = "PRAGMA busy_timeout = 30000;";
            command.ExecuteNonQuery();
        }
    }
}

internal sealed record LoginResponse(string Token);
