using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Abstractions;
using Application.Dtos;
using Application.Filters;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Application;

public class IdempotencyFilterTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static (EndpointFilterInvocationContext Context, FakeIdempotencyRepository Repository) BuildContext(string? key, object?[]? arguments = null)
    {
        var services = new ServiceCollection();
        var repository = new FakeIdempotencyRepository();
        services.AddSingleton<IIdempotencyRepository>(repository);
        services.AddSingleton<IUnitOfWork>(new FakeUnitOfWork());
        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        http.Request.Path = "/accounts/1/movements";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        if (key is not null)
        {
            http.Request.Headers["Idempotency-Key"] = key;
        }

        return (EndpointFilterInvocationContext.Create(http, arguments ?? new object?[] { }), repository);
    }

    private static EndpointFilterDelegate PassThrough() =>
        _ => new ValueTask<object?>(Results.Ok(new { ok = true }));

    private static CreateMovementRequest Request(decimal amount = 10) => new(MovementType.Credit, amount);

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
        repository.Records.Add(IdempotencyRecord.Create("key-1", "/accounts/1/movements", HashOf(Request(10)), 201, "{\"id\":1}"));

        var result = await new IdempotencyFilter(required: true).InvokeAsync(context, PassThrough());

        Assert.Equal(201, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal("{\"id\":1}", (result as ContentHttpResult)?.ResponseContent);
    }

    [Fact]
    public async Task InvokeAsync_ExistingRecord_DifferentRequest_ReturnsConflict()
    {
        var (context, repository) = BuildContext("key-1", new object?[] { 1L, Request(20), null!, null!, default(CancellationToken) });
        repository.Records.Add(IdempotencyRecord.Create("key-1", "/accounts/1/movements", HashOf(Request(10)), 201, "{\"id\":1}"));

        var result = await new IdempotencyFilter(required: true).InvokeAsync(context, PassThrough());

        Assert.Equal(409, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_NoExistingRecord_StoresResponseAndReturnsResult()
    {
        var (context, repository) = BuildContext("key-1", new object?[] { 1L, Request(10), null!, null!, default(CancellationToken) });

        var result = await new IdempotencyFilter(required: true).InvokeAsync(context, PassThrough());

        var record = Assert.Single(repository.Records);
        Assert.Equal("key-1", record.Key);
        Assert.Equal(HashOf(Request(10)), record.RequestHash);
        Assert.Equal(200, record.ResponseStatusCode);
        Assert.False(string.IsNullOrWhiteSpace(record.ResponseBody));
        Assert.Equal(200, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task BeginAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
