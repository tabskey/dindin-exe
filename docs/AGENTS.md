# AGENTS.md

Instruções para qualquer agente de IA (Claude, Copilot, Cursor etc.) que for trabalhar neste repositório.

## Visão geral do projeto

API em C# / .NET (Minimal API) para controle de movimentações de uma conta empresarial (entradas, saídas,
saldo, histórico), com frontend em React. Ver `docs/ARCHITECTURE.md` para o desenho completo do sistema
(camadas, padrões de projeto, modelo de domínio, autenticação, endpoints).

## Princípios inegociáveis

Todo código gerado ou alterado por um agente deve respeitar:

- **Clean Code** — nomes claros, funções pequenas e com uma responsabilidade, sem comentários explicando código mal escrito.
- **SOLID** — principalmente SRP e DIP; interfaces só onde fazem sentido (não crie abstração para um único implementador sem motivo).
- **DRY** — sem duplicar lógica de negócio entre camadas.
- **KISS** — a solução mais simples que resolve o problema. Não adicionar camada, padrão ou dependência "porque pode ser útil depois".
- **Design patterns já definidos** — usar os padrões já decididos no projeto (Strategy para tipos de movimentação, Decorator para auditoria, Idempotency Filter, Repository específico, Result pattern). Não introduzir novos padrões sem seguir o processo abaixo.

## Fluxo de trabalho — commits e PRs

- Uma mudança grande (ex.: uma fase do `docs/API_DEV_CHECKLIST.md`) vira **um commit**.
- A cada **1–2 fases grandes**, abrir **um PR** (não commit avulso por pedaço pequeno de uma fase).
- Só commitar com a verificação verde: `dotnet test` e `dotnet format --verify-no-changes` (backend).
- Quem faz o commit/PR é o responsável pelo projeto; o agente sinaliza prontidão e entrega a
  mensagem de commit e o texto do PR.

## Antes de qualquer mudança estrutural ou drástica

Isso inclui: trocar de banco, trocar de arquitetura, adicionar uma camada nova, adicionar uma dependência
externa não discutida, mudar contrato de endpoint já existente, ou qualquer coisa que não seja um ajuste
pontual dentro do que já foi decidido.

1. **Pare antes de implementar.**
2. **Documente a proposta** em `docs/adr/NNNN-titulo-da-decisao.md` (ver template abaixo), com o problema, as alternativas consideradas e a alternativa proposta.
3. **Consulte o responsável pelo projeto** e aguarde aprovação explícita antes de seguir.
4. Só depois de aprovado, implemente e marque o ADR como `Aceito`.

Mudanças pequenas e already-aligned com o que está documentado (ex: implementar um endpoint já especificado, corrigir um bug, adicionar um teste) não precisam desse processo — só precisam do log de execução abaixo.

## ADRs (Architecture Decision Records)

Toda decisão técnica relevante vira um ADR em `docs/adr/`, numerado sequencialmente:

```
docs/adr/0001-lock-otimista-para-concorrencia.md
docs/adr/0002-jwt-simples-sem-refresh-token.md
```

Template:

```markdown
# NNNN. Título da decisão

Status: Proposto | Aceito | Rejeitado | Substituído por NNNN

## Contexto
Qual problema motivou essa decisão.

## Alternativas consideradas
- Alternativa A — prós e contras
- Alternativa B — prós e contras

## Decisão
O que foi decidido e por quê.

## Consequências
O que fica mais fácil, o que fica mais difícil, o que fica em aberto.
```

## Log de execução do agente

Todo agente deve registrar o que fez em `docs/AGENT_LOG.md`, em ordem cronológica, formato:

```markdown
## 2026-08-28 14:32 — <nome/modelo do agente>
- Ação: implementado endpoint POST /accounts/{id}/movements
- Motivo: requisito funcional do desafio
- Arquivos alterados: Api/Endpoints/MovementEndpoints.cs, Domain/Movement.cs
- Testes: adicionados 3 testes unitários em MovementStrategyTests.cs
- ADR relacionado: nenhum (implementação já especificada em ARCHITECTURE.md)
```

Isso serve tanto como changelog técnico quanto como evidência, na avaliação, de como as decisões foram tomadas.

## Arquivo de arquitetura

`docs/ARCHITECTURE.md` é a fonte de verdade viva do desenho do sistema. Qualquer mudança estrutural aprovada
via ADR deve também atualizar esse arquivo — ele não pode ficar desatualizado em relação ao código.

## Rodando o projeto

Frontend e backend juntos, via Docker Compose:

```bash
docker compose up --build
```

- Frontend: `http://localhost`
- API: `http://localhost/api` (proxy reverso via Nginx)
- Contas de teste (CPF/senha) documentadas no README

Rodando separadamente em desenvolvimento:

```bash
# backend
cd src/backend/Api && dotnet run

# frontend
cd src/frontend && npm install && npm run dev
```

## Testes

```bash
# backend
dotnet test

# frontend
npm test             # Vitest + Testing Library (componentes e regras)
npm run coverage     # cobertura lcov (alimenta o SonarQube)
npx playwright test  # E2E no navegador (requer app + API no ar)
```

Nenhum PR ou entrega deve ser considerada pronta sem os testes unitários do domínio (regra de saldo negativo,
strategies de crédito/débito), os testes de integração dos endpoints, os testes de componentes do frontend
(Vitest + Testing Library) e os E2E (Playwright) passando. Qualidade e cobertura do frontend via SonarQube
local (meta ≥ 80% de linhas) — ADR 0005.

## Escopo do desafio — não expandir sem consulta

Ficaram fora de escopo deliberadamente: hierarquia de contas (pai/filha), múltiplos papéis de usuário,
refresh token, recuperação de senha real. Um agente não deve implementar nenhum desses itens por iniciativa
própria — se parecer necessário, siga o processo de ADR + consulta acima.
