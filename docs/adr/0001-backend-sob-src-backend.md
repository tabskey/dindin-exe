# 0001. Backend isolado em src/backend

Status: Aceito

## Contexto

O repositório tem o frontend isolado em `src/frontend`, mas os projetos do backend estavam soltos na raiz
de `src/` (`Api`, `Application`, `Domain`, `Infrastructure`, `Api.Tests`, `Dindin.slnx`). O usuário
considerou essa organização confusa e pediu para agrupar o backend em uma pasta própria, no mesmo estilo
do frontend.

## Alternativas consideradas

- Manter como está — seguia a árvore original de `ARCHITECTURE.md`, mas deixava `src/` misturando dois
  domínios (backend e frontend).
- Agrupar tudo sob `src/Api/` — centralizava o backend, mas misturava as camadas de arquitetura
  (Domain/Application/Infrastructure) com a camada de apresentação (Api) dentro de um único projeto-filho,
  dificultando enxergar as fronteiras entre camadas.
- **Criar `src/backend/`** — espelha o padrão de `src/frontend/`, deixa a raiz de `src/` limpa e preserva
  as fronteiras entre os projetos das camadas.

## Decisão

Criar a pasta `src/backend/` e mover para ela os projetos `Api`, `Application`, `Domain`, `Infrastructure`,
`Api.Tests` e a solution `Dindin.slnx`. As referências entre projetos são relativas e continuam válidas.
Atualizar os caminhos em `docker-compose.yml`, workflows do GitHub Actions, `AGENTS.md`, `README.md` e
`ARCHITECTURE.md`.

## Consequências
