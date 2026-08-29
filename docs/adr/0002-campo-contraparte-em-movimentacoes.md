# 0002. Campo Contraparte em movimentações

Status: Aceito

## Contexto

O extrato precisa mostrar, além do valor e da direção (entrada/saída), a contraparte da movimentação —
de onde o crédito veio ou para onde o débito foi (ex.: "Ana recebeu do João +50"). No escopo do
exercício não existe contraparte externa ao sistema: a contraparte é ou a própria conta (depósito em
espécie na boca do caixa, rotulado `AUTO-DEPOSITO`) ou outra conta cadastrada no sistema. Todas as
contas do exercício são correntes (`CC`).

## Alternativas consideradas

- **Texto livre informado pelo cliente** — rejeitado: não há cenário de contraparte externa no
  exercício e o texto livre abre espaço para inconsistência; o sistema conhece todas as contas.
- **Referência por `AccountId`** — exige o frontend descobrir o id da contraparte; o CPF é a chave
  natural, única (índice único no schema) e de conhecimento do usuário.
- **Contraparte derivada por CPF + label congelado na criação** — escolhida: a resolução acontece no
  backend (nenhum dado externo entra no contrato), o CPF é único, e o label fica estável no histórico.

## Decisão

- `Movement.Counterparty` (string?, label de exibição) gravado na criação da movimentação.
- `POST /accounts/{id}/movements` passa a aceitar `CounterpartyCpf` (opcional):
  - ausente → auto-depósito: label `AUTO-DEPOSITO {NNN-NN} CC` com o próprio CPF da conta;
  - informado → resolve a conta por CPF; não encontrada → erro `CounterpartyNotFound` (400).
- Formato do label: `{NOME EM MAIÚSCULAS, SEM ACENTO} {CPF mascarado NNN-NN} CC`
  (ex.: `JOAO789-09 CC`), sufixo sempre `CC` (todas as contas do exercício são correntes).
- Máscara do CPF: últimos 5 dígitos → `NNN-NN` (ex.: `123.456.789-09` → `789-09`).
- Seed: Bruno passa a `Checking` (coerência com "todas correntes") e as movimentações de exemplo
  ganham contrapartes (auto-depósito e transferências entre as contas do seed).

## Consequências

- `GET /accounts/{id}/movements` passa a incluir `Counterparty` em cada item do histórico.
- Migração `AddCounterparty`: coluna nullable — movimentações existentes ficam com `null`.
- Frontend (extrato React) ainda não exibe o campo — follow-up fora desta mudança.
