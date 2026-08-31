# 0003. Contraparte por número de conta

Status: Aceito

## Contexto

O modal de depósito do frontend oferecerá a opção "pra quem?" com dois identificadores: CPF ou número
da conta. A ADR 0002 definiu a resolução da contraparte apenas por CPF (`CounterpartyCpf`), mas o
número da conta (`00xxx-xx`) é o identificador amigável que o usuário vê no app e o frontend precisa
poder usá-lo para identificar o destinatário.

## Alternativas consideradas

- **Só CPF na UI** — rejeitada: o usuário pediu também o número da conta como opção de identificação.
- **Índice único em `AccountNumber` + migração** — adiada: a geração aleatória do número torna
  colisões improváveis para o escopo do exercício; o lookup usa `FirstOrDefault`. Reavaliar se o
  número passar a ser escolhido pelo usuário.
- **Campo adicional opcional `CounterpartyAccountNumber`** — escolhida: aditiva, não quebra o
  contrato existente (quem já envia `counterpartyCpf` continua funcionando).

## Decisão

- `CreateMovementRequest` ganha `CounterpartyAccountNumber` (string?, opcional), após `CounterpartyCpf`.
- Resolução no `MovementService`, nesta ordem:
  1. `CounterpartyAccountNumber` não-vazio → resolve a conta pelo número (trim); não encontrada →
     `CounterpartyNotFound` (400);
  2. senão, `CounterpartyCpf` não-vazio → resolução por CPF (comportamento da ADR 0002);
  3. senão → auto-depósito (`AUTO-DEPOSITO`).
- Label inalterado: `CounterpartyLabel.For` mascara o CPF da conta encontrada — o label independe do
  identificador usado na resolução (uniforme e estável no histórico).
- Sem migração: o campo existe só na requisição, sem mudança de schema.

## Consequências

- O frontend poderá enviar o número de conta no depósito (campo opcional, precedência sobre o CPF).
- Testes: 2 unitários no `MovementServiceTests` (label por número, número inexistente mantém saldo) e
  1 de integração em `MovementEndpointTests` (crédito com número de conta de conta registrada → 201).
- Precedência número → CPF → auto-depósito documentada também no README.
