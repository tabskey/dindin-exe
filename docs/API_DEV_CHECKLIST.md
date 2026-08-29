# Checklist — Desenvolvimento da API

Controle das etapas de implementação da API (Minimal API .NET 10), seguindo `docs/AGENTS.md` e
`docs/ARCHITECTURE.md`. Itens marcados com `[x]` estão concluídos. Este arquivo é atualizado junto com o
código, em cada fase.

**Fase atual:** Fase 2 — Infrastructure

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

- [x] Criar solution e projetos: `src/Domain`, `src/Application`, `src/Infrastructure` (class libs) e
      `src/Api.Tests` (xUnit).
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

## Fase 2 — Infrastructure

- [ ] `AppDbContext` + configurações: CPF único, `RowVersion` como token de concorrência,
      `IdempotencyRecord` com PK = `Idempotency-Key`, `AuditLog.Payload` como JSON.
- [ ] Repositórios: `IAccountRepository`, `IMovementRepository` + implementações EF.
- [ ] Seed: contas de teste Ana Teste e Bruno Teste (CPF/senha do README) com hash BCrypt.
- [ ] SQLite: connection string em `appsettings.json` e no container.
- [ ] Critério: `dotnet build` sem erros.

## Fase 3 — Application

- [ ] DTOs: create account, login, movement, balance, histórico paginado.
- [ ] Services: `AccountService` (registro, login, saldo) e `MovementService` (crédito/débito via strategy,
      com retry em `DbUpdateConcurrencyException`).
- [ ] Decorators de auditoria: `AuditedAccountService`, `AuditedMovementService` (gravam `AuditLog`).
- [ ] Idempotency filter (`IEndpointFilter`): opcional em `/accounts`, obrigatório em movimentações.
- [ ] Critério: `dotnet build` sem erros.

## Fase 4 — Api

- [ ] JWT: login confere senha (BCrypt) e devolve token simples (sem refresh/roles).
- [ ] Endpoints documentados + autorização: a conta só acessa os próprios dados (accountId do token vs rota).
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
