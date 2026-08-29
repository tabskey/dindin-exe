# AGENT_LOG

## 2026-08-29 03:05 — Deep Copilot
- Ação: criado o projeto inicial (starter) do backend e do frontend
- Motivo: pedido do usuário — apenas o esqueleto, sem as camadas completas da Clean Architecture por enquanto
- Arquivos alterados: criados `src/Api` (Minimal API .NET 10), `src/frontend` (React 19 + Vite + TypeScript), Dockerfiles e `docker-compose.yml`; removido `UseHttpsRedirection()` do `Program.cs` para o proxy reverso HTTP do Nginx funcionar
- Testes: `dotnet build` (0 erros) e `npm run build` (sucesso)
- ADR relacionado: nenhum (scaffold já especificado em `README.md` / `ARCHITECTURE.md`)

## 2026-08-29 03:20 — Deep Copilot
- Ação: atualizado `README.md` para refletir o estado atual (starter) e corrigido o link do `AGENTS.md`
- Motivo: o README descrevia o alvo final (auth, endpoints, seed, testes, volume SQLite) que ainda não existe no código
- Arquivos alterados: `README.md`
- Testes: nenhum (documentação)
- ADR relacionado: nenhum
