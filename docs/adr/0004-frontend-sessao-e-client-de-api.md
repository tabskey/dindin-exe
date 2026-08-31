# 0004. Frontend: navegação, sessão e client de API

Status: Aceito

## Contexto

O frontend precisa de três infraestruturas para ficar funcional: navegação entre telas (login →
extrato), sessão (o backend devolve um JWT no login, sem refresh token) e chamadas à API (com erros
tipados no corpo `{"error": "..."}`). O app é uma demonstração de nível alto e tende a crescer para
mais telas, então a navegação deve ficar explícita, extensível e com URL por tela desde o início.

## Alternativas consideradas

- **Navegação por estado (sem router)** — rejeitada: com apenas duas telas ela resolveria, mas o
  projeto é uma vitrine de nível alto e a navegação por URL (deep-link, botão voltar, rotas
  nomeadas) é o padrão de mercado em projetos React reais; a fronteira de autenticação fica
  explícita como rota protegida, não como condicional solto no `App`.
- **React Router** — escolhida: biblioteca padrão do ecossistema React, custo baixo, e a estrutura
  de rotas escala para as próximas telas sem retrabalho.
- **Lib de HTTP (axios) ou de dados (react-query)** — rejeitada: o client é pequeno (4 endpoints
  consumidos nesta etapa) e `fetch` nativo cobre o caso; sem camada de cache/estado extra.
- **Token só em memória** — rejeitada: refresh de página perderia a sessão; `localStorage` persiste
  (risco XSS aceito no escopo do exercício, JWT sem refresh fora de escopo).
- **Lib de UI/máscara (headless-ui, imask)** — rejeitada: modal e máscaras (CPF, número de conta,
  moeda) são triviais de implementar; manter dependências mínimas.
- **Framework de testes no frontend (vitest)** — adiado: validação por `npm run build` + `npm run
  lint` + checklist de fluxo manual; adicionar quando houver lógica de UI merecedora de teste.

> Nota anti-over-engineering: a escolha do React Router **não** visa resolver um problema complexo
> de roteamento atual (são duas telas). É decisão deliberada de **consistência e demonstração de
> nível**: URL por tela, deep-link e navegação padrão de mercado, com uso simples — duas rotas e um
> guard de autenticação.

## Decisão

- **Navegação com React Router** (`react-router-dom`): rotas `/login` (pública) e `/extrato`
  (protegida); `Navigate` redireciona para `/extrato` quando autenticado acessa `/login`, e para
  `/login` quando não autenticado acessa `/extrato`. Após login com sucesso, navega para `/extrato`.
- **`AuthContext`**: mantém `token` + `account` (persistidos em `localStorage` — chaves
  `dindin-token` e `dindin-account`), expõe `login(cpf, senha)` (POST `/auth/login`), `logout()`,
  e faz **logout automático** em qualquer resposta `401` de rota autenticada (token expira em 120 min).
- **Client `src/lib/api.ts`**: `fetch` sobre a base relativa `/api` (proxy do Vite em dev → porta
  80 do Docker; nginx em produção), header `Authorization: Bearer`, e parse do corpo
  `{"error": "<mensagem>"}` mapeando por status para mensagens amigáveis em pt-BR.
- **Idempotência nas movimentações**: `Idempotency-Key` com `crypto.randomUUID()` gerada **por
  tentativa**; a mesma chave é reutilizada em retry da mesma tentativa (ex.: falha de rede) e
  regenerada após sucesso — replay não duplica (backend: ADR 0002/0003 + IdempotencyFilter).
- **`Modal` base própria**: overlay, fechar com Esc/clique fora, `aria-modal`, foco no primeiro
  campo, trava de scroll — sem lib de UI.
- **`src/lib/masks.ts`**: máscaras locais (CPF `000.000.000-00`, conta `XXXXX-XX`, valor em R$).

## Consequências

- Nova dependência: `react-router-dom`.
- Navegação por URL (`/login`, `/extrato`), deep-link e botão voltar funcionando; adicionar novas
  telas vira adicionar rotas, sem retrabalho.
- Login deixa de ser troca de estado — vira navegação (`navigate('/extrato')`) após autenticar.
- JWT em `localStorage`: risco de XSS aceito para o escopo (mesmo padrão de apps simples);
  reavaliar (httpOnly cookie) se houver exigência de segurança maior.
- A tela de extrato já consome os tokens de paleta existentes (`balance-bg`, `income`, `expense`).
