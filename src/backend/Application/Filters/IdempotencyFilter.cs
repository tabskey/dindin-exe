using System.Security.Cryptography;
using System.Text.Json;
using Application.Abstractions;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Filters;

// Filtro de idempotência que também é a fronteira transacional das operações de escrita:
// movimentação/auditoria/registro de idempotência são persistidos atomicamente no commit
// (ou desfeitos juntos no rollback). Na presença de Idempotency-Key:
//  - replay com a mesma chave + mesmo request devolve a resposta original sem reexecutar;
//  - mesma chave com request diferente é rejeitada (409);
//  - corrida entre duas requisições com a mesma chave nova é resolvida pela violação de
//    chave única no commit: a perdedora devolve a resposta da vencedora.
public sealed class IdempotencyFilter : IEndpointFilter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly bool _required;

    public IdempotencyFilter(bool required = false) => _required = required;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var hasKey = http.Request.Headers.TryGetValue("Idempotency-Key", out var keyValues)
            && !string.IsNullOrWhiteSpace(keyValues.ToString());

        if (!hasKey && _required)
        {
            return Results.BadRequest(new { error = "Idempotency-Key header is required." });
        }

        var key = hasKey ? keyValues.ToString() : null;
        var requestHash = hasKey ? ComputeRequestHash(context) : null;

        var unitOfWork = http.RequestServices.GetRequiredService<IUnitOfWork>();
        await unitOfWork.BeginAsync(http.RequestAborted);
        try
        {
            if (hasKey)
            {
                var repository = http.RequestServices.GetRequiredService<IIdempotencyRepository>();
                var existing = await repository.GetByKeyAsync(key!, http.RequestAborted);
                if (existing is not null)
                {
                    await unitOfWork.RollbackAsync(http.RequestAborted);
                    return existing.RequestPath != http.Request.Path.ToString() || existing.RequestHash != requestHash
                        ? Results.Conflict(new { error = "Idempotency-Key was already used with a different request." })
                        : Results.Content(existing.ResponseBody, "application/json", statusCode: existing.ResponseStatusCode);
                }
            }

            var result = await next(context);

            if (hasKey)
            {
                var statusCode = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
                var body = (result as IValueHttpResult)?.Value is { } value
                    ? JsonSerializer.Serialize(value, JsonOptions)
                    : string.Empty;

                var repository = http.RequestServices.GetRequiredService<IIdempotencyRepository>();
                await repository.AddAsync(
                    IdempotencyRecord.Create(key!, http.Request.Path.ToString(), requestHash!, statusCode, body),
                    http.RequestAborted);
            }

            await unitOfWork.CommitAsync(http.RequestAborted);
            return result;
        }
        catch (DbUpdateException) when (hasKey)
        {
            // Chave de idempotência já gravada por uma requisição concorrente: desfaz o
            // trabalho duplicado desta transação e devolve a resposta da vencedora.
            await unitOfWork.RollbackAsync(http.RequestAborted);
            using var scope = http.RequestServices.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IIdempotencyRepository>();
            var winner = await repository.GetByKeyAsync(key!, http.RequestAborted);
            if (winner is not null)
            {
                return Results.Content(winner.ResponseBody, "application/json", statusCode: winner.ResponseStatusCode);
            }

            throw;
        }
        catch
        {
            await unitOfWork.RollbackAsync(http.RequestAborted);
            throw;
        }
    }

    // O hash identifica a requisição a partir do DTO já desserializado pelo binding — que,
    // em minimal APIs, roda antes dos endpoint filters (o body bruto já foi consumido nesse
    // ponto). Dessa forma, "mesma chave + request diferente" é rejeitada de forma estável.
    private static string ComputeRequestHash(EndpointFilterInvocationContext context)
    {
        // O pipeline entrega os argumentos espalhados; em alguns contextos (ex.: criação
        // manual do EndpointFilterInvocationContext) eles vêm dentro de um array único.
        var args = context.Arguments.Count == 1 && context.Arguments[0] is object?[] inner
            ? inner
            : context.Arguments.ToArray();

        var request = args.FirstOrDefault(a => a is not null && a.GetType().Namespace == "Application.Dtos");
        return request is null
            ? string.Empty
            : Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions)));
    }
}
