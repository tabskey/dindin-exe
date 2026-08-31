# Project Memory

## Commit/PR Workflow

One commit per big change (e.g. one phase of docs/API_DEV_CHECKLIST.md); one PR every 1-2 phases. Commit only when `dotnet test` and `dotnet format --verify-no-changes` are green. IMPORTANT: the USER performs the commits and PRs themselves — the agent must not commit/push; instead signal readiness and provide the commit message and PR title/description text.

One commit per big change (e.g. one phase of docs/API_DEV_CHECKLIST.md); one PR every 1-2 phases. Commit only when `dotnet test` and `dotnet format --verify-no-changes` are green.

## Frontend — stack e tema

Frontend (src/frontend): React 19 + Vite 8 + TypeScript + Tailwind CSS v4 (plugin @tailwindcss/vite). Tema claro/escuro por classe `.dark` no `<html>` (hook useTheme, localStorage `dindin-theme`, script anti-FOUC no index.html). Cores via tokens semânticos em CSS variables em src/index.css (`:root` claro, `.dark` escuro), mapeadas com `@theme inline`: background (#FFF9E8/#1A1714), surface (#FFFFFF/#25201B), border (#E7C875/#494038), foreground (#25201B/#F7F0E3), muted (#6B5B4B/#B8AA98), accent (#FFB12B nos dois), balance-bg, income/income-bg, expense/expense-bg. Trocar cor = editar só as variáveis. Verificar com `npm run build` e `npm run lint` (na raiz: `npm --prefix src/frontend run build`). Em dev, o Vite proxy `/api` → `http://localhost` (porta 80 do Docker; se a API rodar via `dotnet run`, apontar para :5041 com rewrite — comentado no vite.config.ts).

Frontend (src/frontend): React 19 + Vite 8 + TypeScript + Tailwind CSS v4 (plugin @tailwindcss/vite). Tema claro/escuro por classe `.dark` no `<html>` (hook useTheme, localStorage `dindin-theme`, script anti-FOUC no index.html). Cores via tokens semânticos em CSS variables (`--background`, `--surface`, `--foreground`, `--muted`, `--border`, `--accent`, `--accent-foreground`) definidas em `:root` (claro) e `.dark` (escuro), mapeadas com `@theme inline` em src/index.css. Paleta atual é provisória (neutra) — a paleta final do projeto ainda será definida pelo usuário; trocar = editar só as variáveis. Verificar com `npm run build` e `npm run lint` (na raiz: `npm --prefix src/frontend run build`).

## Frontend — Testes (Vitest/Playwright/SonarQube)

Comandos (na raiz): `npm --prefix src/frontend test` (Vitest+RTL), `npm --prefix src/frontend run coverage` (istanbul → coverage/lcov.info), `npm --prefix src/frontend run test:e2e` (Playwright, sobe o dev server sozinho; requer API no Docker para fluxos autenticados). IMPORTANTE: usar **vitest 3.2.7** — o 4.1.11 quebra neste ambiente (Windows/Node 24/vite 8: "failed to find the runner" até em `describe` puro). Cobertura: provider **istanbul** (o v8 duplica entradas do lcov no Windows) com `all: false` (istanbul `all: true` crasha no Windows) — mede arquivos exercitados; alvo final ≥ 80% de linhas (Fase 6). SonarQube local (sem conta): `docker compose -f docker-compose.sonarqube.yml up -d sonarqube` (porta 9000) + `sonar-project.properties` no src/frontend. Testes de componente vivem junto do arquivo (src/**/*.test.ts(x)); E2E em src/frontend/e2e/.
