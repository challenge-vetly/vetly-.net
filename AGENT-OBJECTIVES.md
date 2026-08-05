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
- [x] Fase 2 — Consentimento LGPD granular: ConsentimentoLgpd (5+1 finalidades),
      revogação com histórico (RN-041..046, RN-084, RN-094)
- [x] Fase 3 — Extensão de Animal: Sexo/PesoKg/Castrado/Condições/Alergias/
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
- **`ConsentimentoLgpd` como entidade standalone, não coleção navegada por
  `Responsavel`** (desvio pontual da Fase 2): o v1 nunca usa navigation
  properties — todo relacionamento é FK (`Guid`) + repositório próprio
  (`IDocumentoRepository`, `IExameRepository` etc.), nunca uma `List<T>` navegada
  a partir do "pai". `ConsentimentoLgpd` ganhou repositório próprio
  (`IConsentimentoLgpdRepository`); as regras (nova concessão sempre cria
  registro; revogação só grava `DataRevogacao`, nunca apaga) viraram métodos em
  `ConsentimentoLgpd` (ctor + `Revogar(agora)`), orquestrados pelo
  `ResponsavelService` — mesmo resultado funcional, mais consistente com o resto
  da base.
- **`Prontuario` ganhou `AlertaSeguranca` (bool, default false)** — pequeno desvio
  pontual da Fase 3 (que nominalmente só toca `Animal`): sem uma classificação no
  próprio `Prontuario`, a invariante RN-088 ("alertas de segurança nunca podem ser
  ocultados", `ANIMAL-002`) não teria como ser verificada de verdade — não existe
  campo algum no v1 que marque um prontuário como alergia/interação. Adição mínima
  (parâmetro opcional no ctor, `default false`, preservado em `CriarCorrecao`), não
  implementa nada da lista FORA DE ESCOPO.
- **Enums trafegam como string no JSON** (`"finalidade": "CompartilhamentoRede"`):
  `Program.cs` ganhou `AddJsonOptions` com `JsonStringEnumConverter` global,
  aplicado a partir da Fase 2. O v1 serializava enums como int; a spec v2 mostra
  valores de enum como strings exatas em todos os payloads de exemplo, e a
  Fase 13 pede uma "tabela de enums (nome → valores aceitos no JSON)" — decisão
  de contrato tomada cedo para não quebrar retroativamente os payloads já
  documentados nas fases seguintes.

## ESTADO ATUAL

**Fase corrente:** Fase 3 concluída (commit a registrar, tag `v2-fase-03-animal`).
Iniciando Fase 4.
**Baseline de testes:** 73/73 verdes (67 unit + 6 integration) — cresceu a partir dos
63 da Fase 2 com `AnimalTests` (5 casos, domínio puro) e `AnimalServiceTests` (5
casos, Moq).
**O que mudou na Fase 3:** `Animal` ganhou `Sexo` (enum novo `SexoAnimal`), `PesoKg`
(decimal?, validado >0 no ctor e em `AtualizarPeso` — `ANIMAL-001`), `Castrado`,
`CondicoesPreExistentes`/`Alergias`/`CarteiraVacinacao`/`MedicacoesEmUso`
(`List<string>`, mesmo padrão de persistência de `AlertasAtivos`), `FotoUrl?`.
Nova entidade `RegistroOcultado` (standalone, mesmo padrão de `ConsentimentoLgpd`)
criada via `Animal.OcultarRegistro(prontuarioId, prontuarioEhAlertaSeguranca, agora)`,
que lança `DomainException("ANIMAL-002", ...)` se o prontuário for alerta de
segurança. `Prontuario` ganhou `AlertaSeguranca` (ver "Decisões de fundação" acima).
`AnimalService.ObterHistoricoAsync` filtra prontuários ocultados quando
`ICurrentUserService.Role == "Veterinario"` (Responsável/Admin sempre veem tudo).
Novos endpoints: `PUT /api/animais/{id}/peso`,
`POST/DELETE /api/animais/{id}/ocultar-registro[/{prontuarioId}]`.
`AnimalService.MapearParaDto` virou `public static` e passou a ser reaproveitado por
`ResponsavelService.ObterAnimaisAsync` e `ConsultaService.ObterBriefingAsync`
(elimina duplicação de mapeamento já sinalizada na exploração inicial). Migration
`Fase03_ExtensaoAnimal`: puramente aditiva (`AddColumn` + `CreateTable
TB_REGISTRO_OCULTADO`), sem backfill de dados necessário.
**Próximos passos:** Fase 4 — máquina de estados da `Consulta`: enum
`StatusConsulta` substituindo os booleanos `Cancelada`/`Finalizada`; métodos de
transição (`IniciarCheckout`, `ConfirmarPagamento`, `MarcarRealizada(vetId)`,
`Cancelar`, `RegistrarNoShowResponsavel`/`RegistrarNoShowVeterinario`) com
`DomainException("CONSULTA-010", ...)` em transição inválida e
`("CONSULTA-011", ...)` em lock expirado; `MarcarRealizada` valida
`vetId == VeterinarioId` via `ICurrentUserService` (`ForbiddenException("ACESSO-002", ...)`
se não bater); novos campos `PreSintomas`/`ContadorRemarcacoes`/
`LockCheckoutExpiraEm`; endpoints
`POST /api/consultas`, `POST /{id}/confirmar-pagamento`, `POST /{id}/realizada`,
`POST /{id}/no-show`, `POST /{id}/remarcar`, `GET /{id}/briefing` estendido.
