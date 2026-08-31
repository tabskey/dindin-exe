# 💰 DinDin.exe

<p align="center">
  <img src="./docs/coin.png" alt="logo" />
</p>

### Sistema de controle de movimentações de conta

**DinDin.exe** é uma aplicação para controle de movimentações de uma conta empresarial, com suporte a entradas, saídas, consulta de saldo e histórico de movimentações — sem nunca permitir saldo negativo. 🤑

O projeto é composto por uma API em **C# / .NET 10 (Minimal API)** e um frontend em **React 19 + Vite + TypeScript**.

> **Status atual:** o backend está completo — regras de negócio, persistência (EF Core + SQLite), autenticação JWT, endpoints, idempotência, auditoria, controle de concorrência e testes (unitários + integração). O frontend ainda é o starter do Vite: as telas de login e extrato são o próximo passo. O andamento detalhado está em [`docs/API_DEV_CHECKLIST.md`](./docs/API_DEV_CHECKLIST.md) e [`docs/AGENT_LOG.md`](./docs/AGENT_LOG.md).

A documentação completa da arquitetura está disponível em [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md), incluindo o diagrama de system design e os ADRs em [`docs/adr/`](./docs/adr/).

As regras para agentes de IA que trabalharem neste repositório estão em [`docs/AGENTS.md`](./docs/AGENTS.md).

---

## 🚀 Rodando o DinDin.exe

### Docker

Pré-requisito: apenas **Docker e Docker Compose** instalados.

Não é necessário instalar o .NET SDK ou Node.js localmente.

```bash
git clone <url-do-repositorio>
cd <pasta-do-repositorio>

docker compose up --build
```

Isso sobe dois containers:

| Serviço      | URL                    | O que é                                                  |
| ------------ | ---------------------- | -------------------------------------------------------- |
| 🖥️ Frontend | `http://localhost`     | React 19 + Vite, build de produção servido via Nginx     |
| 💸 API       | `http://localhost/api` | .NET 10 Minimal API, acessada via proxy reverso do Nginx |

O banco SQLite (`dindin.db`) é persistido em um **volume** (`sqlite-data`): os dados sobrevivem a `docker compose down`. Na primeira subida, migrações e seed (contas de teste) são aplicados automaticamente.

> A chave de assinatura do JWT fica **fora do repositório**: copie `.env.example` para `.env`, gere um valor forte em `Jwt__Key` e rode `docker compose up --build`. Sem o `.env`, a API falha no startup com uma mensagem clara pedindo a chave.

### Parando a aplicação

```bash
docker compose down      # mantém os dados no volume
docker compose down -v   # apaga também o volume (banco zerado)
```

---

## 💻 Rodando sem Docker

Para desenvolvimento local:

```bash
# backend
cd src/backend/Api
dotnet restore
dotnet run
```

Antes do `dotnet run`, defina a variável de ambiente `Jwt__Key` (PowerShell: `$env:Jwt__Key = "<chave-forte>"`).

Em outro terminal:

```bash
# frontend
cd src/frontend
npm install
npm run dev
```

O backend sobe em `http://localhost:5041`, conforme o perfil `http` definido em `src/backend/Api/Properties/launchSettings.json`.

O frontend sobe em `http://localhost:5173`.

Nesse modo não há o proxy reverso do Nginx e, no estado atual, o frontend ainda não realiza chamadas à API (tela de extrato pendente). O fluxo manual completo de requisições está em [`src/backend/Api/Api.http`](./src/backend/Api/Api.http).

---

## 🧪 Contas de teste (seed)

Ao iniciar, o backend cria automaticamente contas de teste (todas correntes) para facilitar a avaliação:

| CPF              | Senha      | Nome         |
| ---------------- | ---------- | ------------ |
| `111.111.111-11` | `senha123` | Ana Teste    |
| `222.222.222-22` | `senha123` | Bruno Teste  |
| `333.333.333-33` | `senha123` | Carlos Teste |

---

## 🔐 Autenticação

Autenticação por **CPF + senha** (hash BCrypt), devolvendo um **JWT**:

```http
POST /auth/login
Content-Type: application/json

{
  "cpf": "111.111.111-11",
  "password": "senha123"
}
```

Resposta:

```json
{
  "token": "<jwt>",
  "account": { "id": 1, "accountNumber": "00315-41", "name": "Ana Teste", "cpf": "111.111.111-11", "accountType": 0 }
}
```

O token deve ser enviado como `Authorization: Bearer <jwt>` nas rotas protegidas, que só permitem acesso aos dados da própria conta (`accountId` do token confere com o da rota).

> 🔒 Aqui o DinDin é seu.
> O DinDin dos outros continua sendo dos outros.

---

## 💸 Endpoints

| Método | Rota                       | Auth | Observação                                                                   |
| ------ | -------------------------- | ---- | ---------------------------------------------------------------------------- |
| `POST` | `/auth/login`              | —    | Autentica por CPF + senha e devolve JWT                                      |
| `POST` | `/accounts`                | —    | Cria uma conta (`Idempotency-Key` opcional)                                  |
| `POST` | `/accounts/{id}/movements` | ✔    | Registra entrada/saída (`Idempotency-Key` obrigatório, `counterpartyCpf` opcional) |
| `GET`  | `/accounts/{id}/balance`   | ✔    | Consulta o saldo disponível                                                  |
| `GET`  | `/accounts/{id}/movements` | ✔    | Consulta o histórico de movimentações paginado                               |
| `POST` | `/accounts/{id}/avatar`    | ✔    | Envia avatar (multipart, JPEG/PNG/WebP até 512 KB)                           |
| `GET`  | `/accounts/{id}/avatar`    | ✔    | Baixa o avatar da conta                                                      |

### Exemplo de movimentação

```http
POST /accounts/1/movements
Authorization: Bearer <token>
Idempotency-Key: 7f2a2e3e-...
Content-Type: application/json

{
  "type": 0,
  "amount": 150.00,
  "counterpartyCpf": "222.222.222-22"
}
```

`type` é numérico: `0` = crédito (entrada), `1` = débito (saída). A chave de idempotência garante que repetir a requisição não duplica a movimentação.

**Contraparte** (quem foi a outra parte da movimentação, exibida no extrato):

- Sem `counterpartyCpf` → depósito na boca do caixa: `AUTO-DEPOSITO 111-11 CC` (o próprio titular).
- Com `counterpartyCpf` → resolve a conta pelo CPF e grava o label (ex.: `BRUNO TESTE 222-22 CC`).
- CPF inexistente → erro `400`.

A regra principal é simples:

**Crédito entra. Débito sai. Saldo negativo não passa.**

---

## 🏗️ Arquitetura

**Clean Architecture enxuta**, evitando camadas e abstrações desnecessárias para o problema:

```text
Api → Application → Domain
              ↑
       Infrastructure
```

Decisões arquiteturais implementadas (motivações em [`docs/adr/`](./docs/adr/)):

* **Strategy** para os tipos de movimentação (crédito/débito);
* **Decorator** para auditoria, desacoplando essa responsabilidade da regra de negócio;
* **Idempotency Filter** (`IEndpointFilter`) evitando duplicidade em operações críticas;
* **Lock otimista** (`RowVersion` no EF Core) protegendo o saldo em movimentações concorrentes;
* **EF Core + SQLite** com migrações e seed no startup;
* **Result pattern** — erros de regra de negócio retornam como valor, não como exception.

A documentação detalhada e o diagrama estão em [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md).

---

## 🧪 Testes

**102 testes, todos verdes** (`dotnet test`):

### Testes unitários (80)

Regras de domínio e serviços: saldo negativo, strategies de crédito/débito, contraparte, auditoria, idempotência, seed e migrações.

### Testes de integração (22)

`WebApplicationFactory` + SQLite (arquivo temporário) exercitando a API real: fluxo completo (criar conta → login → movimentação → saldo → histórico), contraparte, idempotência (replay não duplica), paginação, 401/403/404, avatar e débitos concorrentes nunca negativos.

Cobertura total de linhas: **97,1%** — o CI (`ci-test.yml`) falha se ficar abaixo de 80%.

---

## 📐 Decisões de escopo

Algumas funcionalidades foram avaliadas e ficaram fora do escopo do projeto:

* **Hierarquia de contas** — conta pai visualizando todas as contas filhas;
* **Refresh token e múltiplos papéis de usuário** — JWT simples é suficiente para o escopo;
* **Recuperação de senha real**;
* **Edição/exclusão de conta**, por não fazerem parte do escopo proposto.

---

## 📁 Estrutura do repositório

```text
src/
├── backend/                # Backend .NET (solution + camadas)
│   ├── Api/                # Minimal API .NET 10 (endpoints, JWT, avatar)
│   ├── Application/        # Services, DTOs, decorator de auditoria, idempotency filter
│   ├── Domain/             # Entidades, regras de negócio, strategies
│   ├── Infrastructure/     # EF Core, repositórios, migrações, seed
│   └── Api.Tests/          # Testes unitários e de integração
└── frontend/               # React 19 + Vite + TypeScript (starter)

docs/
├── ARCHITECTURE.md         # Arquitetura completa + diagrama
├── API_DEV_CHECKLIST.md    # Controle das fases de implementação
├── AGENTS.md               # Regras para agentes de IA no repositório
├── AGENT_LOG.md            # Log de execução dos agentes
├── adr/                    # Registro de decisões de arquitetura
├── system-design.png       # Diagrama do system design
└── arquitetura-sistema-conta.pdf

docker-compose.yml          # API + frontend + volume SQLite
```

---

## 🤑 DinDin.exe em uma frase

> **Seu dinheiro, agora em formato executável.**

Ou, como preferimos dizer:

> **Se o saldo ficar negativo, o DinDin.exe falhou.**
