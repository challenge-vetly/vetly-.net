# AGENT-OBJECTIVES.md — Migração Vetly API v1 → v2

## Missão (3 linhas)
Evoluir a API Vetly da especificação v1 para a v2 (`docs/v2-spec/README-PRODUTO-V2.md` +
`docs/v2-spec/README-TECH-V2.md`), preservando integralmente a Clean Architecture e os
padrões já em uso (Factory/Strategy/Repository/DTOs por domínio). Execução incremental
em 13 fases, cada uma com Domain → Application → Infrastructure → API → Tests → build
verde → commit + tag anotada. Nada da lista "FORA DE ESCOPO" abaixo é implementado,
mesmo que uma RN da v2 mencione.

Plano de referência completo (decisões de arquitetura, mapeamento fase→arquivos reais):
`C:\Users\tiago\.claude\plans\prompt-migra-o-shimmering-wirth.md`.

## Checklist das Fases

- [x] Fase 0 — Preparação (branch, este arquivo, infra fundacional: DomainException,
      ForbiddenException, middleware, TimeProvider, ICurrentUserService, claim
      entidadeId no AuthController, baseline build/test)
- [x] Fase 1 — Renomeação Tutor→Responsavel + TierFidelidade/SaldoPontos/
      SaldoCreditosVetly/ContadorNoShows/BloqueadoDescontosAte (RN-064)
- [ ] Fase 2 — Consentimento LGPD granular: ConsentimentoLgpd (5+1 finalidades),
      revogação com histórico (RN-041..046, RN-084, RN-094)
- [ ] Fase 3 — Extensão de Animal: Sexo/PesoKg/Castrado/Condições/Alergias/
      CarteiraVacinacao/MedicacoesEmUso + RegistroOcultado (RN-088, RN-096.2)
- [ ] Fase 4 — Máquina de estados da Consulta: StatusConsulta, lock de checkout,
      no-show, pré-sintomas (RN-057..061)
- [ ] Fase 5 — Pagamento simulado + IComissaoStrategy por plano (RN-037, RN-089)
- [ ] Fase 6 — Cancelamento/no-show v2: crédito de cortesia, strikes, suspensão
      (RN-062..067)
- [ ] Fase 7 — IA v2: decisão Aprovar/NãoAprovar/Corrigir + LogAuditoriaIA
      (RN-096..100)
- [ ] Fase 8 — Colmeia por evento clínico: ConcessaoAcessoProntuario +
      LogAcessoProntuario (RN-083..088)
- [ ] Fase 9 — Avaliação: entidade Avaliacao, moderação, média ponderada
      (RN-076..082)
- [ ] Fase 10 — Fidelidade: ObrigacaoDoPet (Factory por espécie), PontosFidelidade
      FIFO, tiers, IDescontoFidelidadeStrategy (RN-069..075)
- [ ] Fase 11 — Dashboard consolidado do Administrador + FaixaEnterprise +
      autorização por posse via claim entidadeId (RN-007, RN-092, RN-001..006)
- [ ] Fase 12 — Documento: assinatura por nome digitado (RN-031, RN-091)
- [ ] Fase 13 — Documentação final: README de contratos + FLUXO-DE-TESTE.md +
      tag v2.0.0

## FORA DE ESCOPO — NUNCA IMPLEMENTAR

1. **Busca por distância/geolocalização/matching** — RN-047 a RN-055 (raio,
   endereço com lat/long, score, ranking, filtros de busca, slots patrocinados,
   selo "Novo na Vetly").
2. **Comunicação e Notificações** — RN-101 a RN-104, entidade `Notificacao`,
   régua de reenvio, push, inbox, WhatsApp, matriz de canais. Onde um fluxo diz
   "notifica X", registrar só o fato no domínio (flag/log), nunca criar serviço
   de notificação.
3. **Tudo marcado como mock/alvo de produção na spec v2**: Marketplace
   (`Parceiro`, `PedidoMarketplace`, take rate 12%), liquidação financeira real/
   gateway Abacate Pay, assinatura ICP-Brasil, monetização de dados (Níveis 1 e
   2, k-anonimato), integrações Enterprise (labs, NFS-e, ERPs, farmácias,
   calendários), Emergência/SOS, mapa visual.

Se uma regra do escopo depender de algo excluído, implementar só o lado
incluído e registrar a dependência como comentário `// ALVO-PRODUCAO:` + nota no
README de contratos (Fase 13).

## Decisões de fundação já tomadas (não reabrir sem motivo novo)

- Autorização por posse: JWT ganha claim opcional `entidadeId` (decisão
  confirmada com o usuário — extensão do stub de auth existente, sem criar
  entidade `Administrador` nova; "admin de uma empresa" = valor da claim).
- `Vetly.Domain.Exceptions.DomainException(codigo, mensagem)` para invariantes
  de entidade; `ExceptionHandlingMiddleware` mapeia para 422+codigo. Novo
  `ForbiddenException` (Application) mapeia para 403+codigo.
- `TimeProvider` (BCL) singleton; métodos de domínio recebem `DateTime agora`
  explícito — nunca `DateTime.UtcNow` direto em código novo.
- Código de erro `TUTOR-001` renomeado para `RESPONSAVEL-001` na Fase 1.
  Novos códigos `CONSULTA-010/011/012` (evitam colisão com `CONSULTA-001/2/3`
  já existentes).
- `TipoObrigacao` é um enum novo e independente de `TipoLembrete` — não reaproveita
  `LembreteAgendado` (feature v1 intocada).
- Entidades/métodos v1 que já lançam `InvalidOperationException`/
  `ArgumentException` cru (`Pagamento.VincularConsulta`, `Internacao.DarAlta`,
  `Exame.LiberarAoTutor`, `Crmv`) não são refatorados — fora do escopo de cada
  fase.

## ESTADO ATUAL

**Fase corrente:** Fase 1 concluída (commit a registrar, tag `v2-fase-01-responsavel`).
Iniciando Fase 2.
**Baseline de testes:** 54/54 verdes (48 unit + 6 integration) — cresceu a partir dos
51 da Fase 0 com os 3 novos casos de `ResponsavelTests` (janela móvel de no-show).
**O que mudou na Fase 1:** `Tutor`→`Responsavel` renomeado em todas as camadas
(entidade em `src/Vetly.Domain/Entities/Responsavel.cs`, FKs `TutorId`→
`ResponsavelId` em Consulta/Animal/Pagamento/LembreteAgendado, repositório,
`ResponsavelConfiguration`, service, DTOs em `DTOs/Responsavel/`, controller
`ResponsaveisController` com rotas `/api/responsaveis`, código de erro
`TUTOR-001`→`RESPONSAVEL-001`). Novo enum `TierFidelidade`. Novos campos em
`Responsavel`: `TierFidelidade`, `SaldoPontos`, `SaldoCreditosVetly`,
`ContadorNoShows`/`DataUltimoNoShow`/`BloqueadoDescontosAte` + métodos
`RegistrarNoShow(agora)`/`NoShowsAtivos(agora)`. Role `"Responsavel"` adicionada
ao `AuthController`. Migration `Fase01_RenomeiaResponsavel` gerada via
`dotnet ef migrations add` e depois editada à mão para usar `RenameTable`/
`RenameColumn`/`AddColumn` em vez do `DropTable`/`CreateTable` que a scaffold
automática propôs (preserva dados — a scaffold do EF não detecta rename de
*classe*, só de propriedade/coluna, que já veio correta automaticamente).
**Próximos passos:** Fase 2 — consentimento LGPD granular: nova entidade
`ConsentimentoLgpd` (filha de `Responsavel`), enum `FinalidadeConsentimento` (6
valores), métodos `ConcederConsentimento`/`RevogarConsentimento` substituindo os
3 booleanos antigos + `RegistrarConsentimento`; migration de dados migrando os
3 booleanos existentes para registros de consentimento; `ConsultaService.AgendarAsync`
passa a exigir `ConsentimentoAtendimento` ativo (`LGPD-001`); endpoints
`GET/POST /api/responsaveis/{id}/consentimentos` e
`DELETE /api/responsaveis/{id}/consentimentos/{finalidade}`.
