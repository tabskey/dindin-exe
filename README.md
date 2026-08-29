# 💰 DinDin.exe

<p align="center">
  <img src="./docs/coin.png" alt="logo" />
</p>

### Sistema de controle de movimentações de conta

**DinDin.exe** é uma aplicação para controle de movimentações de uma conta empresarial, com previsão de suporte a entradas, saídas, consulta de saldo e histórico de movimentações — sem nunca permitir saldo negativo. 🤑

O projeto é composto por uma API em **C# / .NET 10 (Minimal API)** e um frontend em **React 19 + Vite + TypeScript**.

> **Status atual:** o repositório contém atualmente apenas o *starter* (esqueleto) dos dois projetos. As regras de negócio, autenticação, endpoints, persistência e testes descritos em [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md) ainda **não foram implementados**.

A documentação completa da arquitetura está disponível em [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md), incluindo o diagrama de system design.

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

### Parando a aplicação

```bash
docker compose down
```

---

## 💻 Rodando sem Docker

Para desenvolvimento local:

```bash
# backend
cd src/Api
dotnet restore
dotnet run
```

Em outro terminal:

```bash
# frontend
cd src/frontend
npm install
npm run dev
```

O backend sobe em `http://localhost:5041`, conforme o perfil `http` definido em `src/Api/Properties/launchSettings.json`.

O frontend sobe em `http://localhost:5173`.

Nesse modo não há o proxy reverso do Nginx e, no estado atual do projeto, o frontend ainda não realiza chamadas à API.

---

## 🗺️ O que o DinDin.exe terá?

O alvo final do projeto, detalhado em [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md), contempla:

* Clean Architecture enxuta (`Api → Application → Domain`, com `Infrastructure` implementando as interfaces);
* Autenticação JWT simples por CPF + senha;
* Criação de contas;
* Registro de movimentações de crédito e débito;
* Consulta de saldo;
* Consulta paginada do histórico de movimentações;
* Proteção contra saldo negativo;
* Idempotência para operações críticas;
* Auditoria das movimentações;
* Controle de concorrência;
* EF Core + SQLite;
* Seed de contas para facilitar a avaliação;
* Testes unitários e de integração.

---

## 🔐 Autenticação

A autenticação planejada utiliza **JWT**, com login por CPF e senha.

### Rota planejada

```http
POST /auth/login
```

Exemplo:

```json
{
  "cpf": "111.111.111-11",
  "senha": "senha123"
}
```

> 🔒 Aqui o DinDin é seu.
> O DinDin dos outros continua sendo dos outros.

---

## 💸 Endpoints planejados

> ⚠️ As rotas abaixo representam o contrato definido para a implementação do projeto. **Ainda não estão disponíveis no starter atual.**

| Método | Rota                       | Auth | Observação                                             |
| ------ | -------------------------- | ---- | ------------------------------------------------------ |
| `POST` | `/auth/login`              | —    | Autentica por CPF + senha e devolve JWT                |
| `POST` | `/accounts`                | —    | Cria uma conta (`Idempotency-Key` opcional)            |
| `POST` | `/accounts/{id}/movements` | ✔    | Registra entrada/saída (`Idempotency-Key` obrigatório) |
| `GET`  | `/accounts/{id}/balance`   | ✔    | Consulta o saldo disponível                            |
| `GET`  | `/accounts/{id}/movements` | ✔    | Consulta o histórico de movimentações paginado         |

### Exemplo de movimentação planejada

```http
POST /accounts/{id}/movements
Authorization: Bearer <token>
Idempotency-Key: 7f2a2e3e-...

Content-Type: application/json

{
  "type": "credit",
  "amount": 150.00
}
```

A regra principal é simples:

**Crédito entra. Débito sai. Saldo negativo não passa.**

---

## 🧪 Contas de teste

Como parte da implementação planejada, o projeto terá contas pré-cadastradas (*seed*) para facilitar a avaliação:

| CPF              | Senha      | Nome        |
| ---------------- | ---------- | ----------- |
| `111.111.111-11` | `senha123` | Ana Teste   |
| `222.222.222-22` | `senha123` | Bruno Teste |

> 🔐 Esqueceu a senha? Dirija-se à nossa agência mais próxima. 😄
>
> Brincadeira — não existe fluxo de recuperação de senha neste desafio.

---

## 🏗️ Arquitetura planejada

A arquitetura proposta utiliza uma **Clean Architecture enxuta**, evitando camadas e abstrações desnecessárias para o problema.

```text
Api → Application → Domain
              ↑
       Infrastructure
```

Entre as principais decisões arquiteturais estão:

* **Strategy** para os diferentes tipos de movimentação (crédito/débito);
* **Decorator** para auditoria, desacoplando essa responsabilidade da regra de negócio;
* **Idempotency Filter** (`IEndpointFilter`) para evitar duplicidade em operações críticas;
* **Lock otimista** (`RowVersion` no EF Core) para proteger o saldo em movimentações concorrentes;
* **EF Core + SQLite** para persistência.

A documentação detalhada e o diagrama estão em [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md).

---

## 🧪 Testes planejados

A implementação final contempla:

### Testes unitários

Principalmente para as regras de domínio, incluindo:

* Regra de saldo negativo;
* Movimentações de crédito;
* Movimentações de débito;
* Strategies de movimentação.

### Testes de integração

Utilizando `WebApplicationFactory` + SQLite in-memory para validar os fluxos completos da API:

* Criação de conta;
* Login;
* Autenticação;
* Movimentações;
* Consulta de saldo;
* Consulta de histórico.

---

## 📐 Decisões de escopo

Algumas funcionalidades foram avaliadas e ficaram fora do escopo do projeto:

* **Hierarquia de contas** — conta pai visualizando todas as contas filhas;
* **Refresh token e múltiplos papéis de usuário** — JWT simples é suficiente para o escopo;
* **Recuperação de senha real**;
* **Edição/exclusão de conta**, por não fazerem parte do escopo proposto.

As motivações das principais decisões técnicas estão documentadas em [`docs/adr/`](./docs/adr/).

---

## 📁 Estrutura do repositório

```text
src/
├── Api/                    # Minimal API .NET 10 (starter)
└── frontend/               # React 19 + Vite + TypeScript (starter)

docs/
├── ARCHITECTURE.md         # Arquitetura completa + diagrama
├── AGENTS.md               # Regras para agentes de IA no repositório
├── AGENT_LOG.md            # Log de execução dos agentes
├── system-design.png       # Diagrama do system design
└── arquitetura-sistema-conta.pdf

docker-compose.yml
```

---

## 🤑 DinDin.exe em uma frase

> **Seu dinheiro, agora em formato executável.**

Ou, como preferimos dizer:

> **Se o saldo ficar negativo, o DinDin.exe falhou.**
