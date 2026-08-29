# Sistema de controle de movimentações de conta

API em C# / .NET (Minimal API) para controlar entradas e saídas de uma conta empresarial, com consulta de
saldo e histórico de movimentações — sem nunca permitir saldo negativo. Frontend em React consumindo a API.

> **Status atual:** o repositório contém apenas o *starter* (esqueleto) dos dois projetos. As regras de
> negócio, autenticação, endpoints, persistência e testes descritos em [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md)
> ainda **não** foram implementados.

Documentação completa da arquitetura em [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md) (inclui o diagrama
do system design). Regras para agentes de IA que forem trabalhar neste repositório em
[`docs/AGENTS.md`](./docs/AGENTS.md).

## Rodando o projeto (Docker)

Pré-requisito: apenas Docker e Docker Compose instalados — não precisa de .NET SDK nem Node local.

```bash
git clone <url-do-repositorio>
cd <pasta-do-repositorio>
docker compose up --build
```

Isso sobe dois containers:

| Serviço | URL | O que é |
|---|---|---|
| Frontend | `http://localhost` | React (build de produção), servido via Nginx |
| API | `http://localhost/api` | .NET Minimal API, acessada via proxy reverso do Nginx |

Para parar:

```bash
docker compose down
```

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

O backend sobe em `http://localhost:5041` (perfil `http` em `src/Api/Properties/launchSettings.json`) e o
frontend em `http://localhost:5173`. Nesse modo não há o proxy reverso do Nginx; o frontend ainda não faz
chamadas à API.

## Estrutura do repositório

```
src/
├── Api/                    # Minimal API .NET 10 (starter)
└── frontend/               # React 19 + Vite + TypeScript (starter)
docs/
├── ARCHITECTURE.md          # Arquitetura completa + diagrama
├── AGENTS.md                # Regras para agentes de IA no repositório
├── AGENT_LOG.md             # Log de execução dos agentes
├── system-design.png        # Diagrama do system design
└── arquitetura-sistema-conta.pdf
docker-compose.yml
```

## Planejado (ainda não implementado)

O alvo final, detalhado em [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md), inclui:

- Clean Architecture enxuta (`Api → Application → Domain`, com `Infrastructure` implementando as interfaces).
- Autenticação JWT simples por CPF + senha.
- Endpoints de criação de conta, movimentação, consulta de saldo e histórico.
- EF Core + SQLite, com seed de contas de teste:

| CPF | Senha | Nome |
|---|---|---|
| 111.111.111-11 | senha123 | Ana Teste |
| 222.222.222-22 | senha123 | Bruno Teste |

- Testes unitários (domínio) e de integração (API).
