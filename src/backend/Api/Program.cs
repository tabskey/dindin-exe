using System.Security.Claims;
using System.Text;
using Api.Auth;
using Application.Abstractions;
using Application.Dtos;
using Application.Filters;
using Application.Services;
using Domain.Results;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"))
        .AddInterceptors(new RowVersionInterceptor(), new SqliteBusyTimeoutInterceptor()));

builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IMovementRepository, MovementRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddScoped<IAccountService>(sp =>
    new AuditedAccountService(
        new AccountService(
            sp.GetRequiredService<IAccountRepository>(),
            sp.GetRequiredService<ILogger<AccountService>>()),
        sp.GetRequiredService<IAuditLogRepository>()));
builder.Services.AddScoped<IMovementService>(sp =>
    new AuditedMovementService(
        new MovementService(
            sp.GetRequiredService<IAccountRepository>(),
            sp.GetRequiredService<IMovementRepository>(),
            sp.GetRequiredService<ILogger<MovementService>>()),
        sp.GetRequiredService<IAuditLogRepository>()));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<JwtTokenService>();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Seção 'Jwt' ausente em appsettings.json.");
if (string.IsNullOrWhiteSpace(jwt.Key))
{
    throw new InvalidOperationException(
        "Jwt:Key não configurada. Defina a variável de ambiente Jwt__Key (veja .env.example).");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("sensitive-write", limiter =>
    {
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.PermitLimit = 30;
        limiter.QueueLimit = 0;
    });
});

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async ctx =>
{
    var error = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    ctx.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("GlobalExceptionHandler")
        .LogError(error, "Unhandled exception on {Path}", ctx.Request.Path);

    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { error = "An unexpected error occurred." }));
}));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

using (var scope = app.Services.CreateScope())
{
    DbInitializer.Initialize(scope.ServiceProvider.GetRequiredService<AppDbContext>());
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// POST /accounts — criação de conta (idempotência opcional).
app.MapPost("/accounts", async (CreateAccountRequest request, IAccountService accounts, CancellationToken ct) =>
{
    var result = await accounts.CreateAsync(request, ct);
    return result.IsSuccess
        ? Results.Created($"/accounts/{result.Value!.Id}", result.Value)
        : ToErrorResult(result.Error!);
})
.RequireRateLimiting("sensitive-write")
.AddEndpointFilter(new IdempotencyFilter())
.WithName("CreateAccount");

// POST /auth/login — valida credenciais e devolve o JWT.
app.MapPost("/auth/login", async (LoginRequest request, IAccountService accounts, JwtTokenService tokens, CancellationToken ct) =>
{
    var result = await accounts.LoginAsync(request, ct);
    return result.IsSuccess
        ? Results.Ok(new LoginResponse(tokens.CreateToken(result.Value!), result.Value!))
        : Results.Unauthorized();
})
.RequireRateLimiting("sensitive-write")
.WithName("Login");

// POST /accounts/{id}/movements — idempotência obrigatória.
app.MapPost("/accounts/{accountId:long}/movements", async (long accountId, CreateMovementRequest request,
    ClaimsPrincipal user, IMovementService movements, CancellationToken ct) =>
{
    if (!IsOwner(user, accountId))
    {
        return Results.Forbid();
    }

    var result = await movements.CreateAsync(accountId, request, ct);
    return result.IsSuccess
        ? Results.Created($"/accounts/{accountId}/movements/{result.Value!.Id}", result.Value)
        : ToErrorResult(result.Error!);
})
.RequireAuthorization()
.AddEndpointFilter(new IdempotencyFilter(required: true))
.WithName("CreateMovement");

// GET /accounts/{id}/balance
app.MapGet("/accounts/{accountId:long}/balance", async (long accountId, ClaimsPrincipal user,
    IAccountService accounts, CancellationToken ct) =>
{
    if (!IsOwner(user, accountId))
    {
        return Results.Forbid();
    }

    var result = await accounts.GetBalanceAsync(accountId, ct);
    return result.IsSuccess ? Results.Ok(result.Value) : ToErrorResult(result.Error!);
})
.RequireAuthorization()
.WithName("GetBalance");

// GET /accounts/{id}/movements — histórico paginado.
app.MapGet("/accounts/{accountId:long}/movements", async (long accountId, ClaimsPrincipal user,
    IMovementService movements, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
{
    if (!IsOwner(user, accountId))
    {
        return Results.Forbid();
    }

    var result = await movements.GetHistoryAsync(accountId, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);
    return result.IsSuccess ? Results.Ok(result.Value) : ToErrorResult(result.Error!);
})
.RequireAuthorization()
.WithName("GetMovementHistory");

// POST /accounts/{id}/avatar — upload multipart (idempotência opcional).
app.MapPost("/accounts/{accountId:long}/avatar", async (long accountId, IFormFile? file, ClaimsPrincipal user,
    IAccountService accounts, CancellationToken ct) =>
{
    if (!IsOwner(user, accountId))
    {
        return Results.Forbid();
    }

    if (file is null)
    {
        return Results.BadRequest(new { error = "Avatar file is required." });
    }

    if (file.Length > AccountService.MaxAvatarBytes)
    {
        return Results.BadRequest(new { error = $"Avatar must be up to {AccountService.MaxAvatarBytes / 1024} KB." });
    }

    using var stream = new MemoryStream();
    await file.CopyToAsync(stream, ct);
    var result = await accounts.UpdateAvatarAsync(accountId, stream.ToArray(), file.ContentType ?? string.Empty, ct);
    return result.IsSuccess ? Results.NoContent() : ToErrorResult(result.Error!);
})
.DisableAntiforgery()
.RequireAuthorization()
.AddEndpointFilter(new IdempotencyFilter())
.WithName("UploadAvatar");

// GET /accounts/{id}/avatar — bytes da imagem com content type.
app.MapGet("/accounts/{accountId:long}/avatar", async (long accountId, ClaimsPrincipal user,
    IAccountService accounts, CancellationToken ct) =>
{
    if (!IsOwner(user, accountId))
    {
        return Results.Forbid();
    }

    var result = await accounts.GetAvatarAsync(accountId, ct);
    return result.IsSuccess
        ? Results.File(result.Value!.Data, result.Value.ContentType)
        : ToErrorResult(result.Error!);
})
.RequireAuthorization()
.WithName("GetAvatar");

static bool IsOwner(ClaimsPrincipal user, long accountId) =>
    long.TryParse(user.FindFirstValue("accountId"), out var claimId) && claimId == accountId;

static IResult ToErrorResult(DomainError error) => error.Code switch
{
    DomainErrorCode.AccountNotFound or DomainErrorCode.AvatarNotFound => Results.NotFound(new { error = error.Message }),
    DomainErrorCode.CpfAlreadyRegistered => Results.Conflict(new { error = error.Message }),
    DomainErrorCode.AccountNumberCollision => Results.Json(new { error = error.Message }, statusCode: StatusCodes.Status503ServiceUnavailable),
    DomainErrorCode.InvalidCredentials => Results.Unauthorized(),
    _ => Results.BadRequest(new { error = error.Message })
};

app.Run();

record LoginResponse(string Token, AccountDto Account);

// Expõe a classe Program para os testes de integração (WebApplicationFactory<Program>).
public partial class Program { }
