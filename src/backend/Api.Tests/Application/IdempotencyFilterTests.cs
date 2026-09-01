using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Abstractions;
using Application.Dtos;
using Application.Filters;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Application;

public class IdempotencyFilterTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static (EndpointFilterInvocationContext Context, FakeIdempotencyRepository Repository) BuildContext(
        string? key, object?[]? arguments = null, string? accountId = null, IUnitOfWork? unitOfWork = null)
    {
        var services = new ServiceCollection();
        var repository = new FakeIdempotencyRepository();
        services.AddSingleton<IIdempotencyRepository>(repository);
        services.AddSingleton<IUnitOfWork>(unitOfWork ?? new FakeUnitOfWork());
        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        http.Request.Path = "/accounts/1/movements";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        if (accountId is not null)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("accountId", accountId) }, "test"));
        }

        if (key is not null)
        {
            http.Request.Headers["Idempotency-Key"] = key;
        }

        return (EndpointFilterInvocationContext.Create(http, arguments ?? new object?[] { }), repository);
    }

    private static EndpointFilterDelegate PassThrough() =>
        _ => new ValueTask<object?>(Results.Ok(new { ok = true }));

    private static CreateMovementRequest Request(long amount = 10) => new(MovementType.Credit, amount);

    private static string HashOf(object request) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions)));

    [Fact]
    public async Task InvokeAsync_MissingKeyWhenRequired_ReturnsBadRequestAndSkipsEndpoint()
    {
        var (context, repository) = BuildContext(key: null);

        var result = await new IdempotencyFilter(required: true).InvokeAsync(context, PassThrough());

        Assert.Equal(400, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Empty(repository.Records);
    }

    [Fact]
    public async Task InvokeAsync_MissingKeyWhenOptional_InvokesEndpoint()
    {
        var (context, _) = BuildContext(key: null);

        var result = await new IdempotencyFilter(required: false).InvokeAsync(context, PassThrough());

        Assert.Equal(200, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ExistingRecord_ReplaysStoredResponse()
    {
        var (context, repository) = BuildContext("key-1", new object?[] { 1L, Request(10), null!, null!, default(CancellationToken) });
        repository.Records.Add(IdempotencyRecord.Create("anon:key-1", "/accounts/1/movements", HashOf(Request(10)), 201, "{\"id\":1}"));

        var result = await new IdempotencyFilter(required: true).InvokeAsync(context, PassThrough());

        Assert.Equal(201, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal("{\"id\":1}", (result as ContentHttpResult)?.ResponseContent);
    }

    [Fact]
    public async Task InvokeAsync_ExistingRecord_DifferentRequest_ReturnsConflict()
    {
        var (context, repository) = BuildContext("key-1", new object?[] { 1L, Request(20), null!, null!, default(CancellationToken) });
        repository.Records.Add(IdempotencyRecord.Create("anon:key-1", "/accounts/1/movements", HashOf(Request(10)), 201, "{\"id\":1}"));

        var result = await new IdempotencyFilter(required: true).InvokeAsync(context, PassThrough());

        Assert.Equal(409, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_NoExistingRecord_StoresResponseAndReturnsResult()
    {
        var (context, repository) = BuildContext("key-1", new object?[] { 1L, Request(10), null!, null!, default(CancellationToken) });

        var result = await new IdempotencyFilter(required: true).InvokeAsync(context, PassThrough());

        var record = Assert.Single(repository.Records);
        Assert.Equal("anon:key-1", record.Key);
        Assert.Equal(HashOf(Request(10)), record.RequestHash);
        Assert.Equal(200, record.ResponseStatusCode);
        Assert.False(string.IsNullOrWhiteSpace(record.ResponseBody));
        Assert.Equal(200, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_ScopesKeyPerAccount()
    {
        var (context, repository) = BuildContext("key-1", new object?[] { 1L, Request(10), null!, null!, default(CancellationToken) }, accountId: "42");
        repository.Records.Add(IdempotencyRecord.Create("42:key-1", "/accounts/1/movements", HashOf(Request(10)), 201, "{\"id\":1}"));

        var result = await new IdempotencyFilter(required: true).InvokeAsync(context, PassThrough());

        Assert.Equal(201, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_AnonymousRecord_IsNotReplayedForAuthenticatedUser()
    {
        var (context, repository) = BuildContext("key-1", new object?[] { 1L, Request(10), null!, null!, default(CancellationToken) }, accountId: "42");
        repository.Records.Add(IdempotencyRecord.Create("anon:key-1", "/accounts/1/movements", HashOf(Request(10)), 201, "{\"id\":1}"));

        var result = await new IdempotencyFilter(required: true).InvokeAsync(context, PassThrough());

        Assert.Equal(200, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(2, repository.Records.Count);
    }

    [Fact]
    public async Task InvokeAsync_UniqueViolationOnCpf_ReturnsConflictAndRollsBack()
    {
        var unitOfWork = new ThrowingUnitOfWork("UNIQUE constraint failed: Accounts.Cpf");
        var (context, _) = BuildContext(
            "key-1", new object?[] { 1L, Request(10), null!, null!, default(CancellationToken) }, unitOfWork: unitOfWork);

        var result = await new IdempotencyFilter(required: true).InvokeAsync(context, PassThrough());

        Assert.Equal(409, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(1, unitOfWork.RollbackCount);
    }

    [Fact]
    public async Task InvokeAsync_UniqueViolationOnAccountNumber_Returns503AndRollsBack()
    {
        var unitOfWork = new ThrowingUnitOfWork("UNIQUE constraint failed: Accounts.AccountNumber");
        var (context, _) = BuildContext(
            "key-1", new object?[] { 1L, Request(10), null!, null!, default(CancellationToken) }, unitOfWork: unitOfWork);

        var result = await new IdempotencyFilter(required: true).InvokeAsync(context, PassThrough());

        Assert.Equal(503, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(1, unitOfWork.RollbackCount);
    }

    [Fact]
    public async Task InvokeAsync_FailingEndpoint_DoesNotCacheFailure()
    {
        var (context, repository) = BuildContext(
            "key-1", new object?[] { 1L, Request(10), null!, null!, default(CancellationToken) });
        EndpointFilterDelegate failing = _ => new ValueTask<object?>(Results.BadRequest(new { error = "x" }));

        var result = await new IdempotencyFilter(required: true).InvokeAsync(context, failing);

        Assert.Equal(400, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Empty(repository.Records);
    }

    [Fact]
    public async Task InvokeAsync_CreateAccountRequest_PasswordDoesNotAffectHash()
    {
        var requestWithOtherPassword = new CreateAccountRequest("Ana Teste", "111.111.111-11", AccountType.Checking, "outra-senha");
        var (context, repository) = BuildContext("key-1", new object?[] { requestWithOtherPassword });
        // Hash gravado com a MESMA request, mas com a senha anonimizada — se a senha entrasse
        // no hash, esse replay divergiria (409). Sem ela, o replay bate.
        repository.Records.Add(IdempotencyRecord.Create(
            "anon:key-1", "/accounts/1/movements", HashOf(new CreateAccountRequest("Ana Teste", "111.111.111-11", AccountType.Checking, "***")), 201, "{\"id\":1}"));

        var result = await new IdempotencyFilter(required: true).InvokeAsync(context, PassThrough());

        Assert.Equal(201, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_AvatarUpload_HashesFileContent()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        using var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, stream.Length, "file", "avatar.png") { Headers = new HeaderDictionary() };
        var (context, repository) = BuildContext("key-1", new object?[] { file });
        repository.Records.Add(IdempotencyRecord.Create(
            "anon:key-1", "/accounts/1/movements", Convert.ToHexString(SHA256.HashData(bytes)), 201, "{\"ok\":true}"));

        var result = await new IdempotencyFilter(required: true).InvokeAsync(context, PassThrough());

        Assert.Equal(201, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    private sealed class ThrowingUnitOfWork : IUnitOfWork
    {
        private readonly string _commitError;

        public ThrowingUnitOfWork(string commitError) => _commitError = commitError;

        public int RollbackCount { get; private set; }

        public Task BeginAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("Commit falhou", new Exception(_commitError));

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task BeginAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
