# 0005. Testes e qualidade no frontend

Status: Aceito

## Contexto

O frontend acumulou lógica com regras reais: máscaras (CPF, conta, moeda), sessão (`AuthContext`),
`Modal` acessível, login, criação de conta e, nas fases seguintes, extrato e movimentação com
contraparte. Validar apenas com `npm run build`/`npm run lint` + checklist manual (decisão adiada
no ADR 0004) não escala mais. Chegou o momento previsto no ADR 0004 ("adicionar quando houver
lógica de UI merecedora de teste"): testes de componentes/regras, testes E2E no navegador e análise
de qualidade com cobertura.

## Alternativas consideradas

- **Jest + Testing Library** — pedido inicialmente pelo usuário; trocado por Vitest na revisão: em
  projeto Vite, o Jest exige transform adicional (ts-jest/Babel/SWC) e configuração separada, sem
  ganho funcional sobre o Vitest.
- **Vitest + Testing Library** — escolhido: mesma API e superfície do Jest (`describe`/`it`/`expect`,
  mocks com `vi`), roda nativamente sobre o Vite 8 do projeto (sem transform extra), ambiente jsdom
  para componentes React e cobertura nativa (lcov para o SonarQube). Testes escritos para o Jest
  migram sem reescrever a lógica dos casos.
- **Cypress vs Playwright** — Playwright escolhido: multi-navegador, ideal para fluxos E2E completos
  (login → extrato → movimentação), com tracing/debug de primeira classe.
- **SonarCloud vs SonarQube local** — SonarCloud exige conta/org/token; escolhido **SonarQube local
  (Community) via Docker**, autocontido e sem conta, consistente com o restante do projeto.

## Decisão

- **Vitest + Testing Library** para componentes e regras: `@testing-library/react` (render/screen),
  `@testing-library/jest-dom` (matchers), `@testing-library/user-event` (interação), ambiente
  `jsdom`; mocks com `vi` (`vi.fn`/`vi.mock`).
- **Playwright** (`@playwright/test`) para E2E: fluxos completos no navegador (login → extrato →
  depósito/saque; criar conta → login preenchido), contra o app com a API no ar (Docker ou dev).
- **SonarQube local** via `docker-compose.yml` (serviço `sonarqube`, Community, sem conta) +
  `sonar-project.properties` consumindo `coverage/lcov.info` do Vitest; meta de cobertura de linhas
  **≥ 80%**, alinhada à do backend.
- Scripts no `package.json`: `test` (vitest run), `test:watch`, `coverage` (com lcov),
  `test:e2e` (playwright test).

## Consequências

- Novas devDependencies: `vitest`, `@vitest/coverage-v8`, `@testing-library/react`,
  `@testing-library/jest-dom`, `@testing-library/user-event`, `jsdom`, `@playwright/test`.
- Cobertura exportada em `coverage/lcov.info` e consumida pelo SonarQube.
- E2E exige app + API no ar e seed carregado — documentado no AGENTS.md.
- A decisão "testes adiados" do ADR 0004 fica substituída por este ADR.
- Em aberto (não bloqueia): gate de cobertura no CI do frontend (hoje a análise é local, como era
  o backend antes do `ci-test.yml`).
