using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Abstractions;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Filters;

public sealed class IdempotencyFilter : IEndpointFilter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly bool _required;

    public IdempotencyFilter(bool required = false) => _required = required;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (!http.Request.Headers.TryGetValue("Idempotency-Key", out var keyValues) || string.IsNullOrWhiteSpace(keyValues.ToString()))
        {
            return _required
                ? Results.BadRequest(new { error = "Idempotency-Key header is required." })
                : await next(context);
        }

        var key = keyValues.ToString();
        var repository = http.RequestServices.GetRequiredService<IIdempotencyRepository>();
        var existing = await repository.GetByKeyAsync(key, http.RequestAborted);
        if (existing is not null)
        {
            return Results.Content(existing.ResponseBody, "application/json", statusCode: existing.ResponseStatusCode);
        }

        var requestHash = await ComputeRequestHashAsync(http.Request, http.RequestAborted);
        var result = await next(context);
        var statusCode = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
        var body = (result as IValueHttpResult)?.Value is { } value
            ? JsonSerializer.Serialize(value, JsonOptions)
            : string.Empty;

        await repository.AddAsync(
            IdempotencyRecord.Create(key, http.Request.Path.ToString(), requestHash, statusCode, body),
            http.RequestAborted);
        await repository.SaveChangesAsync(http.RequestAborted);

        return result;
    }

    private static async Task<string> ComputeRequestHashAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken);
        request.Body.Position = 0;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
    }
}
