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
- [x] Fase 4 — Máquina de estados da Consulta: StatusConsulta, lock de checkout,
      no-show, pré-sintomas (RN-057..061)
- [x] Fase 5 — Pagamento simulado + IComissaoStrategy por plano (RN-037, RN-089)
- [x] Fase 6 — Cancelamento/no-show v2: crédito de cortesia, strikes, suspensão
      (RN-062..067)
- [x] Fase 7 — IA v2: decisão Aprovar/NãoAprovar/Corrigir + LogAuditoriaIA
      (RN-096..100)
- [x] Fase 8 — Colmeia por evento clínico: ConcessaoAcessoProntuario +
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
- **`Consulta.Finalizar()`/`Finalizada` (v1) foi fundido em `MarcarRealizada` (v2)**:
  o v1 tinha um conceito de "finalizada" (receita assinada) distinto de qualquer
  "realizada". A spec v2 só fala em `StatusConsulta.Realizada`. Pela ordem descrita
  no Fluxo 4 do produto (documentos + assinatura acontecem *antes* do vet marcar
  "realizada"), mantive a exigência de receita assinada (RN-031) dentro de
  `ConsultaService.MarcarRealizadaAsync`, em vez de criar um estado extra. Também
  adicionei a checagem de posse ali (`ForbiddenException("ACESSO-002", ...)` se
  `ICurrentUserService.EntidadeId` existir e não bater com `VeterinarioId`) — a
  entidade `Consulta.MarcarRealizada` em si só valida a transição de estado, não
  quem está chamando (mantém Domain sem depender de identidade de request).
- **`CriarConsultaDto` perdeu `PagamentoId`**: no v1, agendar exigia um pagamento
  *já confirmado antes*. No v2 (RN-058), a consulta nasce em `EmCheckout` sem
  pagamento nenhum; o pagamento é criado e confirma o estado depois, numa etapa
  separada (Fase 5). O `POST /api/consultas/{id}/confirmar-pagamento` desta fase é
  a transição de estado pura; a Fase 5 vai enriquecê-la com cálculo de comissão.
- **RN-015 (pagamento pré-confirmado obrigatório) e o código `CONSULTA-001`
  (consulta já cancelada) foram retirados** — mudança legítima de regra v1→v2: a
  máquina de estados unificada usa `CONSULTA-010` para qualquer transição inválida
  a partir de qualquer estado (inclusive cancelar uma consulta já cancelada), então
  `CONSULTA-001` ficou redundante. Testes antigos que dependiam desses dois foram
  atualizados/removidos (ver corpo do commit da Fase 4).
- **`LogAuditoriaIA` tem um único método de mutação (`RegistrarDecisao`), chamável
  uma única vez** — desvio pontual do texto literal "imutável — sem métodos de
  mutação após criação" (Fase 7). Justificativa: o próprio contrato de endpoints
  exige isso — `POST .../ia/diagnostico` devolve um `logId` no momento da
  sugestão, e `POST .../ia/decisao` não recebe nenhum id nem o conteúdo sugerido
  de volta (só `tipo`/`decisao`/`conteudoCorrigido`). Isso só fecha se o log
  nascer "pendente" na sugestão (com o conteúdo sugerido já gravado) e for
  finalizado na decisão (a `ObterPendenteAsync` busca o pendente mais recente por
  consulta+tipo). Uma segunda chamada a `RegistrarDecisao` lança
  `DomainException("IA-002", ...)` — depois de finalizado, o registro é imutável
  de verdade. Artefatos sem decisão de vet (`DocumentoGerado`, gerados via
  `LogAuditoriaIA.RegistrarArtefatoAutomatico`) nascem já finalizados, sem passar
  pelo ciclo pendente.
- **`PodeGerarDocumentos()` trocou o gate de `DiagnosticoValidado` (bool, v1) para
  `EstadoFinalDefinido` (bool, v2)** — evolução explícita da RN-024 pedida na
  Fase 7. O erro também mudou: `DocumentoService.GerarAsync` agora lança
  `BusinessRuleException("CONSULTA-012", ...)` em vez de `"RN-024"`.
  `DiagnosticoValidado`/`ValidarDiagnostico()` (v1) continuam existindo e
  funcionando — só pararam de ser o gate de geração de documentos.
- **Enums trafegam como string no JSON** (`"finalidade": "CompartilhamentoRede"`):
  `Program.cs` ganhou `AddJsonOptions` com `JsonStringEnumConverter` global,
  aplicado a partir da Fase 2. O v1 serializava enums como int; a spec v2 mostra
  valores de enum como strings exatas em todos os payloads de exemplo, e a
  Fase 13 pede uma "tabela de enums (nome → valores aceitos no JSON)" — decisão
  de contrato tomada cedo para não quebrar retroativamente os payloads já
  documentados nas fases seguintes.
- **Revogar `CompartilhamentoRede` (Fase 2) não revoga em cascata as
  `ConcessaoAcessoProntuario` já concedidas** (desvio pontual da Fase 8, onde o
  plano original cogitava isso). Justificativa: cada concessão já nasce com
  `ExpiraEm` curto (fim do ciclo da consulta + 24h) — o pior caso de exposição
  é o mesmo já assumido pela janela normal de acesso, então uma varredura
  ativa em `RevogarConsentimentoAsync` para revogar concessões futuras
  adicionaria complexidade (nova dependência de `IConcessaoAcessoProntuarioRepository`
  dentro de `ResponsavelService`, cruzando um limite de domínio que hoje não
  existe) sem mudar o pior caso real de exposição de dados. Revogar o
  consentimento apenas impede a criação de **novas** concessões a partir da
  próxima consulta confirmada (`ConcederAcessoPorConsultaAsync` checa o
  consentimento ativo no momento da confirmação) — concessões já emitidas
  seguem seu próprio ciclo de vida via `EstaAtiva(agora)`/`ExpiraEm`.

## ESTADO ATUAL

**Fase corrente:** Fase 8 concluída (commit a registrar, tag
`v2-fase-08-colmeia`). Iniciando Fase 9.
**Baseline de testes:** 150/150 verdes (144 unit + 6 integration) — cresceu a
partir dos 137 da Fase 7 com `ConcessaoAcessoProntuarioTests` (4 casos,
domínio puro), `AcessoProntuarioServiceTests` (6 casos, Moq) e reescrita de
`AnimalServiceTests`/`ConsultaServiceTests` para cobrir acesso completo ×
restrito × negado.
**O que mudou na Fase 8:** novas entidades `ConcessaoAcessoProntuario`
(`AnimalId`, `VeterinarioId`, `ConsultaId`, `BaseAcesso` enum
`{ConsentimentoRede, AtendimentoDireto}`, `ConcedidoEm`, `ExpiraEm`,
`Revogada`, com `EstaAtiva(agora)`/`Revogar()`) e `LogAcessoProntuario`
(somente inserção). Novo `AcessoProntuarioService` (`PodeAcessarAsync`,
`TemAcessoCompletoAsync`, `RegistrarAcessoAsync`,
`ConcederAcessoPorConsultaAsync`, `ObterConcessoesAtivasAsync`,
`ObterLogAcessosAsync`). `ConsultaService.ConfirmarPagamentoAsync` chama
`ConcederAcessoPorConsultaAsync` após confirmar (cria concessão só se o
Responsável tem `CompartilhamentoRede` ativo — Fase 2 — com
`ExpiraEm = dataConsulta.AddHours(24)`); `ObterBriefingAsync` agora exige
`PodeAcessarAsync` (403 `ACESSO-001` se negado) e registra log. `IConsultaRepository`
ganhou `ExisteConsultaAsync` (base "atendimento direto" — vet já atendeu o
animal alguma vez) e `IAnimalRepository` ganhou
`ObterHistoricoLongitudinalPorVeterinarioAsync` (join com `Consultas` — histórico
restrito ao que o vet chamador produziu). `AnimalService.ObterHistoricoAsync`
reescrito: chamador `Veterinario` sem nenhum acesso → 403 `ACESSO-001`;
com concessão ativa → histórico completo (menos ocultados, Fase 3); só
"atendimento direto" → apenas os próprios prontuários, sem checar ocultados
(vet nunca vê o que não produziu, ocultado ou não); chamador `Responsavel`
sempre vê tudo, sem consultar a colmeia. Novo
`AnimalService.ObterLogAcessosAsync`. Endpoints novos:
`GET /api/animais/{id}/log-acessos`,
`GET /api/veterinarios/{id}/concessoes`. Migration
`Fase08_ColmeiaAcessoProntuario`: puramente aditiva (`CreateTable` para as
duas tabelas novas, sem nenhum rename — scaffold do EF não teve ambiguidade
desta vez).
**Próximos passos:** Fase 9 — avaliação e notoriedade: nova entidade
`Avaliacao` (`ConsultaId` único, `ResponsavelId`, `VeterinarioId`,
`NotaGeral` 1-5 obrigatória, subnotas opcionais, `Comentario`, `Data`,
`StatusModeracao` enum, `RespostaVeterinario` 0..1, `Invalidada`). Regras via
`DomainException`: fora da janela de 7 dias (`AVALIACAO-001`), consulta não
realizada (`AVALIACAO-002`), edição após 48h (`AVALIACAO-003`), segunda
resposta do vet (`AVALIACAO-004`). `Veterinario` ganha
`NotaMedia`/`TotalAvaliacoes` com recálculo ponderado por recência (últimos
90 dias pesam 2×), só exposto com ≥3 avaliações. Cancelamento/reembolso
invalida avaliação e recalcula (RN-081, reaproveita `ICancelamentoStrategy`
da Fase 6). Endpoints `POST /api/consultas/{id}/avaliacao`,
`PUT /api/avaliacoes/{id}`, `POST /{id}/resposta`, `POST /{id}/moderar`,
`GET /api/veterinarios/{id}/avaliacoes`.
