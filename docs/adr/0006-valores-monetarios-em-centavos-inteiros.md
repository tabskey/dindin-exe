# 0006. Valores monetários em centavos inteiros

Status: Aceito

## Contexto

O backend tratava dinheiro como `decimal` (reais) em `Movement.Amount`, `Account.Balance`, nos DTOs
(`CreateMovementRequest`, `MovementDto`, `BalanceDto`) e nas strategies de crédito/débito. No SQLite,
`decimal` com `HasPrecision(18, 2)` vira coluna `TEXT` (sem `NUMERIC` nativo), e o frontend convertia
texto mascarado ↔ `Number` (float binário) — o que reintroduz risco de arredondamento justamente na
camada de exibição/entrada.

A máscara decimal "natural" também era ambígua: digitar `50` podia significar R$ 50,00 ou R$ 0,50
dependendo da vírgula, e o auto-completar dos centavos adicionava estado extra no componente.

## Alternativas consideradas

- **Manter `decimal` + máscara decimal natural com auto-completar** — rejeitado: mantém o float no
  client (arredondamento) e a ambiguidade de digitação.
- **`decimal` no backend + conversão apenas no client** — rejeitado: não resolve o armazenamento
  `TEXT` do SQLite nem a precisão de ponta a ponta.
- **Centavos inteiros (`long`) de ponta a ponta** — escolhido: dinheiro como inteiro de centavos em
  todas as camadas, sem float; SQLite armazena `INTEGER`; a máscara volta a ser centavos-based
  ("cada dígito digitado é um centavo") e fica sem ambiguidade.

## Decisão

- **Backend**: `Movement.Amount` e `Account.Balance` passam a ser `long` (centavos); DTOs
  (`CreateMovementRequest.Amount`, `MovementDto.Amount`, `BalanceDto.Balance`) também `long`;
  strategies e `Movement.Create` recebem `long`. Removido `HasPrecision(18, 2)`.
- **Seed** em centavos (Ana: 1050,00 → `105000`, Bruno: 80,00 → `8000`, etc.).
- **Migração recriada** (`InitialCreate` única): SQLite não altera tipo de coluna; o banco de
  desenvolvimento é recriável (seed repopula).
- **Frontend**: `maskBRL` centavos-based (cada dígito = centavo, sempre duas casas) +
  `parseBRLToCents` (string → centavos inteiros), substituindo `parseBRL` (float); exibição divide
  por 100 com `Intl.NumberFormat`.

## Consequências

- A API passa a receber/devolver `amount`/`balance` como inteiros de centavos (ex.: R$ 150,00 →
  `15000`). Clientes devem enviar/interpretar centavos.
- Bancos de desenvolvimento existentes precisam ser recriados (`docker compose down -v` ou apagar
  `dindin.db`); o seed repopula as contas de teste.
- Sem representação float de dinheiro em nenhuma camada; testes de máscara e de componentes
  atualizados para a nova semântica.
