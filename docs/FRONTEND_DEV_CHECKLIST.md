# Checklist — Desenvolvimento do Frontend

Controle das etapas de implementação do frontend (React 19 + Vite + TypeScript + Tailwind CSS v4),
seguindo `docs/AGENTS.md`. Itens marcados com `[x]` estão concluídos. Este arquivo é atualizado junto
com o código, em cada fase.

**Fase atual:** 4 — Extrato mínimo (próxima)

## Regras aplicáveis (AGENTS.md)

- Clean Code, KISS — sem abstrações além das documentadas.
- Nenhuma mudança estrutural sem ADR + aprovação (ADR 0004 para sessão/client de API; ADR 0005 para
  testes).
- Validação: `npm run build` (tsc + vite) e `npm run lint` (eslint) verdes; testes de
  componentes/regras com Vitest + Testing Library (`npm test`) e E2E com Playwright
  (`npx playwright test`); qualidade e cobertura via SonarQube (meta ≥ 80% de linhas) — ver
  ADR 0005 (substitui o "testes adiados" do ADR 0004).
- Registrar cada ação em `docs/AGENT_LOG.md`.

## Escopo (fechado)

- Fluxo ponta a ponta: login → extrato mínimo (saldo + histórico) → movimentação via modal.
- Criação de conta via modal (na tela de login).
- Movimentação única (depósito/saque) via FAB "+": depósito com contraparte opcional por
  **CPF** ou **número de conta** (backend já suporta ambos — ADR 0003).
- Testes: Vitest + Testing Library (componentes e regras), Playwright (fluxos E2E) e SonarQube
  local (qualidade + cobertura ≥ 80%) — ADR 0005.
- Fora de escopo (não implementar sem consulta): avatar na UI, paginação do histórico, saque com
  contraparte, refresh token, recuperação de senha.

---

## Fase 0 — Decisões de arquitetura (ADR 0004)

- [x] Redigir ADR 0004 com as decisões: react-router (rotas `/login` e `/extrato`, guard de
      autenticação — decisão de consistência/nível, não over-engineering); `AuthContext`
      (token + conta em `localStorage dindin-token`); client `src/lib/api.ts` (fetch sobre `/api`,
      parse de `{"error": ...}` → mensagens amigáveis); idempotência com `crypto.randomUUID()` por
      tentativa (reuso em retry da mesma tentativa; regenera após sucesso); `Modal` base acessível.
- [x] Aprovação do usuário antes da implementação.
- [ ] Critério: decisões registradas e aprovadas.

## Fase 1 — Infra de API e sessão

- [x] Instalar `react-router-dom`; `BrowserRouter` com rotas `/login` (pública) e `/extrato`
      (protegida) + redirects por estado de autenticação.
- [x] `src/lib/api.ts`: client fetch com base `/api` (relativo — proxy do Vite em dev, nginx em
      Docker), header `Authorization: Bearer`, parse de `{"error": "<mensagem>"}` por status
      (409 → "CPF já cadastrado", 401 → "CPF ou senha inválidos", 400 → mensagem do backend,
      401 em rota autenticada → logout).
- [x] Tipos dos DTOs espelhando o backend: `AccountDto`, `LoginResponse`, `BalanceDto`,
      `MovementDto`, `MovementHistoryDto`, `CreateMovementRequest`.
- [x] `AuthContext`: sessão (token + conta), `login()`, `logout()`, 401 → logout automático.
- [x] `LoginPage`: submit real — estado de loading (botão desabilitado), erro inline; navega para
      `/extrato` após login; `ExtratoPage` esqueleto com "Sair" (preenchida na Fase 4).
- [x] Critério: `npm run build` e `npm run lint` verdes; `POST /api/auth/login` do seed via proxy
      (nginx em `:80`) → HTTP 200 (curl); validação visual do login no browser fica com o usuário
      (`npm run dev` + API no Docker).

## Fase 2 — Modal base + Criar conta

- [x] `Modal` base: overlay, fecha com Esc e clique fora, `aria-modal`, foco no primeiro campo,
      trava de scroll (`src/components/Modal.tsx`).
- [x] `src/lib/masks.ts`: `maskCpf` (em uso no login e no criar conta) e `maskAccountNumber`
      (`XXXXX-XX`, usada na Fase 5).
- [x] `CreateAccountModal`: nome, CPF (mascarado), senha (≥ 6); validações locais; erro 409
      inline ("CPF já cadastrado"); sucesso → fecha o modal e **pré-preenche o CPF no login
      (senha vazia)** via callback `onCreated`.
- [x] Critério: `npm run build` e `npm run lint` verdes; fluxo "criar conta → login preenchido"
      validado visualmente pelo usuário.

## Fase 3 — Infra de testes (Vitest + Testing Library, Playwright, SonarQube)

- [x] ADR 0005 marcado como Aceito (Vitest + Testing Library, Playwright e SonarQube local via
      Docker, meta ≥ 80% de linhas).
- [x] Vitest + Testing Library: `vitest`, `@vitest/coverage-istanbul`, `@testing-library/react`,
      `@testing-library/jest-dom`, `@testing-library/user-event`, `jsdom`; `vitest.config.ts` com
      `environment: jsdom`, setup com jest-dom e `coverage.reporter` incluindo `lcov`.
- [x] Scripts no `package.json`: `test` (vitest run), `test:watch`, `coverage` (vitest run
      --coverage), `test:e2e` (playwright test).
- [x] Playwright: `@playwright/test` + `playwright.config.ts` (baseURL + webServer ou uso do
      Docker); instalar browser (`npx playwright install chromium`).
- [x] SonarQube local: serviço `sonarqube` no `docker-compose.sonarqube.yml` (Community, sem conta) +
      `sonar-project.properties` apontando para `coverage/lcov.info`.
- [x] Testes de regressão do que já existe: `masks.ts` (CPF/conta), `Modal` (Esc, clique fora,
      foco), `LoginPage` (submit, erro 401, pré-preenchimento pós-criar conta) e
      `CreateAccountModal` (validações, erro 409).
- [x] Critério: `npm test` verde e smoke de `test:e2e`; `npm run coverage` gera `lcov.info`.

## Fase 4 — Extrato mínimo

- [ ] `ExtratoPage`: card de saldo (`bg-balance-bg`), lista de movimentações (receita
      `bg-income-bg`/`text-income`, despesa `bg-expense-bg`/`text-expense`; data, contraparte,
      valor), botão **sair**.
- [ ] Carregamento de dados: `GET /accounts/{id}/balance` + `GET /accounts/{id}/movements?page=1`;
      estado de carregamento e erro simples.
- [ ] FAB "+" fixo (canto inferior direito).
- [ ] Testes Vitest+RTL do `ExtratoPage`: saldo e as 8 movimentações com estilos de receita/
      despesa; estados de carregamento e erro; botão sair (logout).
- [ ] Critério: login com Ana → saldo inicial e as 8 movimentações do seed renderizadas com os
      estilos de receita/despesa; `npm test` + `npm run build`/`lint` verdes.

## Fase 5 — Movimentação (modal único depósito/saque)

- [ ] FAB "+" → escolha **Depósito | Saque**.
- [ ] `MovementModal`: valor em R$ (máscara de moeda); se **depósito** → seção "Pra quem?" com
      seletor **CPF | Número da conta**:
      - CPF → um campo com máscara `000.000.000-00`;
      - Conta → dois campos (número `XXXXX` + dígito `XX`) combinados em `XXXXX-XX`;
      - vazio → auto-depósito (boca do caixa).
      Se **saque** → apenas valor.
- [ ] Idempotência: `Idempotency-Key` com `crypto.randomUUID()` por tentativa (reuso em retry da
      mesma tentativa; regenera após sucesso) — replay não duplica.
- [ ] Estados: loading (botão desabilitado), erro inline (ex.: "Contraparte não encontrada"),
      sucesso → confirmação (valor + novo saldo) e refresh do extrato.
- [ ] Testes Vitest+RTL do `MovementModal`: depósito (CPF | número da conta | vazio) e saque;
      `Idempotency-Key` por tentativa (mesma chave em retry, nova após sucesso); estados
      loading/erro/sucesso.
- [ ] Critério: depósito com CPF, com número de conta e vazio (auto-depósito) atualizam saldo e
      histórico; saque com saldo insuficiente → erro amigável sem quebrar o extrato; `npm test`
      verdes.

## Fase 6 — Validação final e documentação

- [ ] Fluxo manual completo no Docker: `docker compose up --build` (imagem do frontend nova) e
      execução do fluxo login → criar conta → depósito → saque.
- [ ] `npm run build`, `npm run lint` e `npm test` verdes.
- [ ] Playwright E2E completo no navegador: login → extrato → depósito → saque, e
      criar conta → login preenchido.
- [ ] SonarQube: análise executada com cobertura ≥ 80% (meta alinhada ao backend) e sem
      problemas novos de qualidade bloqueantes.
- [ ] ADR 0004 e ADR 0005 finalizadas com as decisões como executadas.
- [ ] Atualizar `README.md` (status do frontend + seção de testes) e `docs/AGENT_LOG.md`;
      marcar itens deste checklist.
