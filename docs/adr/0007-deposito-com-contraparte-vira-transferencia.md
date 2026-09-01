# 0007. Depósito com contraparte vira transferência

Status: Aceito

## Contexto

A ADR 0002 definiu a contraparte como metadado informativo (label de exibição): um depósito
com `CounterpartyCpf` creditava a própria conta do depositante e apenas registrava o nome do
destinatário no extrato. A UI, porém, rotula o campo como "Pra quem?", criando a expectativa de
que o dinheiro vá para a outra pessoa. Na prática, "depositar para João" creditava Ana — o
usuário reportou como erro.

## Alternativas consideradas

- **Transferência (débito no remetente + crédito no destinatário)** — escolhida: bate com o
  "pra quem?" da UI e com o modelo de banco digital ("enviar dinheiro"). O depósito sem
  contraparte continua sendo o auto-depósito (boca do caixa).
- **Crédito só no destinatário** (depósito em espécie em conta de terceiro) — rejeitada: o
  remetente não veria a saída no extrato e a tela de confirmação (saldo do remetente) ficaria
  confusa.
- **Manter como label** — rejeitada: o comportamento não corresponde à expectativa da UI.

## Decisão

- `POST /accounts/{id}/movements` com `type = crédito` e contraparte (CPF ou número da conta)
  executa uma **transferência**:
  1. débito na conta autenticada (saldo insuficiente → `InsufficientBalance`, 400);
  2. crédito na conta do destinatário;
  3. dois movimentos em uma transação (atomicidade via UnitOfWork do filtro de idempotência),
     com retry de concorrência otimista recarregando **ambas** as contas;
  4. resposta = o movimento de débito do remetente (contraparte = destinatário).
- Saque com contraparte → `InvalidRequest` (400) — contraparte só existe em depósito.
- Transferir para si mesmo → `InvalidRequest` (400) — usar depósito sem contraparte.
- Labels: débito `{DESTINATARIO 00XXX-XX CC}`, crédito `{REMETENTE 00XXX-XX CC}`.

### Atualização: label usa o número da conta (supera a ADR 0002 neste ponto)

O label de contraparte passou a exibir o **número da conta** (`00XXX-XX`) em vez do fragmento de
CPF mascarado (`NNN-NN`): o número é único por construção (índice único + retry), enquanto a
máscara de 5 dígitos podia se repetir entre contas (ex.: `233.333.333-33` e `333.333.333-33`
ambos viravam `333-33`), fazendo o extrato parecer ter um "número repetido". `CounterpartyLabel`
agora usa `account.AccountNumber`; `MaskCpf` foi removido.

## Consequências

- `MovementService.CreateAsync` ganhou `CreateTransferAsync` e um `PersistWithRetryAsync`
  que recarrega e reaplica os strategies de todas as contas mutadas em conflito de concorrência.
- O extrato de cada conta mostra o seu lado da transferência (saída no remetente, entrada no
  destinatário).
- Testes: unitários (transferência por CPF/número, saldo insuficiente, para si mesmo, débito
  com contraparte, retry de concorrência em duas contas), integração
  (`Transfer_WithCounterpartyCpf_MovesMoneyBetweenAccounts`,
  `Transfer_WithCounterpartyAccountNumber_MovesMoneyBetweenAccounts`,
  `Debit_WithCounterparty_ReturnsBadRequest`) e E2E (`transferência: o valor sai do remetente
  e cai no destinatário`).
- ADR 0002/0003 ficam parcialmente superadas: a resolução da contraparte continua válida; a
  semântica "label informativo" dá lugar à transferência quando há contraparte no depósito; o
  formato do label agora usa o número da conta (único), não o CPF mascarado.
