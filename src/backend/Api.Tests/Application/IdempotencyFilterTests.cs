using System.Security.Cryptography;
using System.Text;
using Application.Abstractions;
using Application.Filters;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Application;

public class IdempotencyFilterTests
{
    private static (EndpointFilterInvocationContext Context, FakeIdempotencyRepository Repository) BuildContext(string? key, string body = "{}")
    {
        var services = new ServiceCollection();
        var repository = new FakeIdempotencyRepository();
        services.AddSingleton<IIdempotencyRepository>(repository);
        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        http.Request.Path = "/accounts/1/movements";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        if (key is not null)
        {
            http.Request.Headers["Idempotency-Key"] = key;
        }

        return (EndpointFilterInvocationContext.Create(http, new object?[] { }), repository);
    }

    private static EndpointFilterDelegate PassThrough() =>
        _ => new ValueTask<object?>(Results.Ok(new { ok = true }));

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
        var (context, repository) = BuildContext("key-1");
        repository.Records.Add(IdempotencyRecord.Create("key-1", "/accounts/1/movements", "hash", 201, "{\"id\":1}"));

        var result = await new IdempotencyFilter(required: true).InvokeAsync(context, PassThrough());

        Assert.Equal(201, (result as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal("{\"id\":1}", (result as ContentHttpResult)?.ResponseContent);
    }

    [Fact]
    public async Task InvokeAsync_NoExistingRecord_StoresResponseAndReturnsResult()
    {
        var (context, repository) = BuildContext("key-1");

        var result = await new IdempotencyFilter(required: true).InvokeAsync(context, PassThrough());

        var record = Assert.Single(repository.Records);
        Assert.Equal("key-1", record.Key);
        Assert.Equal(200, record.ResponseStatusCode);
        Assert.False(string.IsNullOrWhiteSpace(record.ResponseBody));
        Assert.Equal(200, (result as IStatusCodeHttpResult)?.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ComputesHashOfRequestBody_AndKeepsBodyReadable()
    {
        const string body = "{\"type\":1,\"amount\":10}";
        var (context, repository) = BuildContext("key-1", body);
        string? seenByEndpoint = null;
        EndpointFilterDelegate next = ctx =>
        {
            using var reader = new StreamReader(ctx.HttpContext.Request.Body);
            seenByEndpoint = reader.ReadToEnd();
            return new ValueTask<object?>(Results.Ok(new { ok = true }));
        };

        await new IdempotencyFilter(required: true).InvokeAsync(context, next);

        var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        Assert.Equal(expectedHash, repository.Records[0].RequestHash);
        Assert.Equal(body, seenByEndpoint);
    }
}
