# Sistema de controle de movimentações de conta

API em C# / .NET (Minimal API) para controlar entradas e saídas de uma conta empresarial, com consulta de
saldo e histórico de movimentações — sem nunca permitir saldo negativo. Frontend em React consumindo a API.

Documentação completa da arquitetura em [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md) (inclui o diagrama
do system design). Regras para agentes de IA que forem trabalhar neste repositório em [`AGENTS.md`](./AGENTS.md).

## Rodando o projeto (Docker)

Pré-requisito: apenas Docker e Docker Compose instalados — não precisa de .NET SDK nem Node local.

```bash
git clone <url-do-repositorio>
cd <pasta-do-repositorio>
docker compose up --build
```

Isso sobe dois containers e um volume:

| Serviço | URL | O que é |
|---|---|---|
| Frontend | `http://localhost` | React, servido via Nginx |
| API | `http://localhost/api` | .NET Minimal API, acessada via proxy reverso do Nginx |

O banco SQLite é persistido em um volume Docker, então os dados sobrevivem a um `docker compose down` /
`up` (a menos que o volume seja removido explicitamente com `-v`).

Para parar:

```bash
docker compose down
```

Para resetar os dados (remove o volume também):

```bash
docker compose down -v
```

## Contas de teste (seed)

A aplicação sobe com contas pré-cadastradas para facilitar a avaliação sem precisar criar conta manualmente:

| CPF | Senha | Nome |
|---|---|---|
| 111.111.111-11 | senha123 | Ana Teste |
| 222.222.222-22 | senha123 | Bruno Teste |

> Esqueceu a senha? Dirija-se à nossa agência mais próxima. 😄 (brincadeira — não há fluxo de recuperação
> de senha neste desafio, ver seção de escopo abaixo.)

## Rodando sem Docker (desenvolvimento)

```bash
# backend
cd src/Api
dotnet restore
dotnet run

# frontend (em outro terminal)
cd src/frontend
npm install
npm run dev
```

Nesse modo, ajuste a URL da API no `.env` do frontend, já que não há o proxy reverso do Nginx fazendo isso
automaticamente.

## Autenticação

```bash
POST /auth/login
{ "cpf": "111.111.111-11", "senha": "senha123" }
```

Devolve um JWT. Envie esse token no header `Authorization: Bearer <token>` nas demais chamadas — cada conta
só acessa os próprios dados.

## Endpoints

| Método | Rota | Auth | Observação |
|---|---|---|---|
| `POST` | `/auth/login` | — | Autentica por CPF + senha, devolve JWT |
| `POST` | `/accounts` | — | Cria conta (`Idempotency-Key` opcional no header) |
| `POST` | `/accounts/{id}/movements` | ✔ | Registra entrada/saída (`Idempotency-Key` **obrigatório**) |
| `GET` | `/accounts/{id}/balance` | ✔ | Consulta saldo disponível |
| `GET` | `/accounts/{id}/movements` | ✔ | Histórico de movimentações, paginado |

Exemplo de registro de movimentação:

```bash
POST /accounts/{id}/movements
Authorization: Bearer <token>
Idempotency-Key: 7f2a2e3e-...
{ "type": "credit", "amount": 150.00 }
```

## Arquitetura, em resumo

Minimal API com Clean Architecture enxuta (`Api → Application → Domain`, `Infrastructure` implementando as
interfaces), sem camadas ou abstrações além do que o problema pede. Detalhes completos, incluindo o
diagrama, em [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md).

- **Strategy** para os tipos de movimentação (crédito/débito)
- **Decorator** para auditoria, desacoplada da regra de negócio
- **Idempotency Filter** (`IEndpointFilter`) para evitar duplicidade de operações críticas
- **Lock otimista** (RowVersion no EF Core) para evitar saldo negativo em movimentações concorrentes
- **EF Core + SQLite**, com seed das contas de teste

## Testes

```bash
cd src/Api.Tests
dotnet test
```

- Testes unitários no domínio, cobrindo principalmente a regra de saldo negativo e as strategies de
  crédito/débito.
- Testes de integração na API via `WebApplicationFactory` + SQLite in-memory, cobrindo os fluxos completos
  (criação de conta, login, movimentação, consulta).

## Decisões de escopo (o que ficou de fora, de propósito)

- Hierarquia de contas (conta pai enxergando todas as filhas) — avaliado e descartado por não ser realista
  para o cenário proposto.
- Refresh token, múltiplos papéis de usuário — JWT simples é suficiente para o escopo.
- Recuperação de senha real — fora do escopo do desafio.
- Edição/exclusão de conta — não pedido no enunciado.

Motivação de cada decisão técnica relevante está documentada como ADR em `docs/adr/`.

## Estrutura do repositório

```
src/
├── Api/                    # Minimal API, endpoints, filtros, DI
├── Application/            # Services, DTOs, decorator de auditoria
├── Domain/                 # Entidades, regras de negócio, strategies
├── Infrastructure/         # EF Core, repositórios, seed
├── Api.Tests/               # Testes unitários e de integração
└── frontend/                # React
docs/
├── ARCHITECTURE.md          # Arquitetura completa + diagrama
├── system-design.png        # Diagrama do system design
└── adr/                      # Architecture Decision Records
AGENTS.md                     # Regras para agentes de IA no repositório
docker-compose.yml
```
