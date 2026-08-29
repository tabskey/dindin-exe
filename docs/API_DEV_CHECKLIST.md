# Checklist — Desenvolvimento da API

Controle das etapas de implementação da API (Minimal API .NET 10), seguindo `docs/AGENTS.md` e
`docs/ARCHITECTURE.md`. Itens marcados com `[x]` estão concluídos. Este arquivo é atualizado junto com o
código, em cada fase.

**Fase atual:** Fase 4 — Api

## Regras aplicáveis (AGENTS.md)

- Clean Code, SOLID (SRP e DIP), DRY, KISS — sem abstrações além das documentadas.
- Padrões já definidos: Strategy (tipos de movimentação), Decorator (auditoria), Idempotency Filter,
  Repository específico, Result pattern.
- Testes obrigatórios antes de considerar pronto: unitários do domínio e integração da API.
- Cobertura de código: meta ≥ 80% de linhas.
- Registrar cada ação em `docs/AGENT_LOG.md`.
- Nenhuma mudança estrutural sem ADR + aprovação. Esta fase dispensa ADR: tudo já especificado em
  `ARCHITECTURE.md`.

## Escopo (fechado)

- Endpoints: `POST /accounts`, `POST /auth/login`, `POST /accounts/{id}/movements`,
  `GET /accounts/{id}/balance`, `GET /accounts/{id}/movements`.
- Fora de escopo (não implementar sem consulta): hierarquia de contas, múltiplos papéis, refresh token,
  recuperação de senha.

---

## Fase 0 — Estrutura da solução ✅

- [x] Criar solution e projetos: `src/backend/Domain`, `src/backend/Application`, `src/backend/Infrastructure`
      (class libs) e `src/backend/Api.Tests` (xUnit).
- [x] Referências: `Api → Application/Domain/Infrastructure`; `Infrastructure → Domain/Application`;
      `Api.Tests → Api` (WebApplicationFactory).
- [x] Pacotes: `Microsoft.EntityFrameworkCore.Sqlite` (Infrastructure), `Microsoft.AspNetCore.Authentication.JwtBearer`
      e `BCrypt.Net-Next` (Api), pacotes de teste (Api.Tests).
- [x] Critério: `dotnet build` na solution sem erros.

## Fase 1 — Domain ✅

- [x] Entidades (ARCHITECTURE.md §4): `Account` (com `RowVersion` para lock otimista), `Movement`,
      `AuditLog`, `IdempotencyRecord`; enum `AccountType` (cosmético).
- [x] Strategies (ARCHITECTURE.md §3): `CreditStrategy` e `DebitStrategy` com a regra de saldo negativo.
- [x] Result pattern: `Result` / `Result<T>` para erros de negócio (saldo insuficiente, CPF duplicado...).
- [x] Testes unitários do domínio: regra de saldo negativo + strategies de crédito/débito.
- [x] Critério: `dotnet test` passando (22 testes) e cobertura do Domain em **97,36%** (meta ≥ 80%).

## Fase 2 — Infrastructure ✅

- [x] `AppDbContext` + configurações: CPF único, `RowVersion` como token de concorrência
      (`IsConcurrencyToken` + `RowVersionInterceptor` — SQLite não gera rowversion nativo),
      `IdempotencyRecord` com PK = `Idempotency-Key`, `AuditLog.Payload` como TEXT (JSON).
- [x] Repositórios: `IAccountRepository`, `IMovementRepository` + implementações EF.
- [x] Migração EF Core `InitialCreate` (schema; design-time factory + tool local `dotnet-ef` via
      `.config/dotnet-tools.json`) — app usa `Migrate()` + `Seed()` na inicialização.
- [x] Seed: contas Ana/Bruno/Carlos Teste (CPF `xxx.xxx.xxx-xx`, senha `senha123`, serial `00xxx-xx`) e
      8 movimentações com saldos consistentes (inclui caso de borda: Carlos zera o saldo — negativo nunca).
- [x] SQLite: connection string em `appsettings.json` e no container (`docker-compose.yml`).
- [x] Critério: `dotnet build` sem erros — 30 testes passando (8 novos de persistência).

## Fase 3 — Application ✅

- [x] DTOs: create account, login, movement, balance, histórico paginado.
- [x] Services: `AccountService` (registro, login, saldo) e `MovementService` (crédito/débito via strategy,
      com retry em `DbUpdateConcurrencyException`).
- [x] Decorators de auditoria: `AuditedAccountService`, `AuditedMovementService` (gravam `AuditLog`).
- [x] Idempotency filter (`IEndpointFilter`): opcional em `/accounts`, obrigatório em movimentações.
- [x] Critério: `dotnet build` sem erros — 61 testes passando; cobertura Application 97,51% (total 46,62%
      com Api ainda em 0%).

## Fase 4 — Api

- [ ] JWT: login confere senha (BCrypt) e devolve token simples (sem refresh/roles).
- [ ] Endpoints documentados + autorização: a conta só acessa os próprios dados (accountId do token vs rota).
- [ ] Avatar: `POST /accounts/{id}/avatar` (multipart, máx. 512 KB, JPEG/PNG/WebP) e
      `GET /accounts/{id}/avatar` (stream) — sem idempotency filter.
- [ ] DI e organização dos endpoints (Program.cs).
- [ ] Critério: fluxo manual via `Api.http` / curl funcionando localmente.

## Fase 5 — Testes de integração

- [ ] `WebApplicationFactory` + SQLite in-memory: fluxos completos (criar conta → login → movimentação →
      saldo → histórico).
- [ ] Idempotência: segunda chamada com a mesma chave não duplica.
- [ ] Concorrência: débitos concorrentes nunca geram saldo negativo.
- [ ] Critério: `dotnet test` 100% verde e cobertura geral ≥ 80%.

## Fase 6 — Docker e documentação

- [ ] Volume SQLite no `docker-compose.yml` (persistência, ARCHITECTURE.md §9) + rebuild + verificação via proxy.
- [ ] Atualizar `README.md`: mover itens implementados para fora de "Planejado".
- [ ] Registrar todo o andamento em `docs/AGENT_LOG.md`.
- [ ] Critério: `docker compose up --build` com os dois serviços e API respondendo.
