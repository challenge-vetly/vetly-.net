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
- **Enums trafegam como string no JSON** (`"finalidade": "CompartilhamentoRede"`):
  `Program.cs` ganhou `AddJsonOptions` com `JsonStringEnumConverter` global,
  aplicado a partir da Fase 2. O v1 serializava enums como int; a spec v2 mostra
  valores de enum como strings exatas em todos os payloads de exemplo, e a
  Fase 13 pede uma "tabela de enums (nome → valores aceitos no JSON)" — decisão
  de contrato tomada cedo para não quebrar retroativamente os payloads já
  documentados nas fases seguintes.

## ESTADO ATUAL

**Fase corrente:** Fase 4 concluída (commit a registrar, tag `v2-fase-04-consulta`).
Iniciando Fase 5.
**Baseline de testes:** 102/102 verdes (96 unit + 6 integration) — cresceu a partir
dos 73 da Fase 3 com `ConsultaStateMachineTests` (20 casos, domínio puro,
transições válidas/inválidas) e a reescrita de `ConsultaServiceTests` (17 casos
cobrindo LGPD-001, RN-057, confirmar-pagamento, cancelar, marcar-realizada com
posse, no-show, remarcar).
**O que mudou na Fase 4:** `Consulta` reescrita em torno de `StatusConsulta`
(`EmCheckout/Confirmada/Realizada/Cancelada/NoShowResponsavel/NoShowVeterinario`)
substituindo os booleanos `Cancelada`/`Finalizada` e o campo `StatusPagamento`.
Novos campos `TipoServico` (enum novo), `PreSintomas`, `ContadorRemarcacoes`,
`LockCheckoutExpiraEm`, `DataRealizada`. Métodos de transição
(`IniciarCheckout`, `ConfirmarPagamento`, `Cancelar`, `MarcarRealizada`,
`RegistrarNoShowResponsavel`/`Veterinario`, `Reagendar`) lançam
`DomainException("CONSULTA-010", ...)` em transição inválida e
`("CONSULTA-011", ...)` em lock de checkout expirado (ver "Decisões de fundação"
acima para os desvios de `FinalizarAsync`→`MarcarRealizadaAsync`, remoção de
`PagamentoId` do agendamento e retirada de `CONSULTA-001`/RN-015). Novo enum
`ParteNoShow`. `ConsultaService.AgendarAsync` valida consentimento clínico
(LGPD-001, já existia) e agora também RN-057 (serviço físico exige presencial).
Novos endpoints: `POST /api/consultas/{id}/confirmar-pagamento`,
`POST /{id}/realizada` (substitui `/finalizar`), `POST /{id}/no-show`,
`POST /{id}/remarcar`; `GET /{id}/briefing` agora inclui `preSintomas`.
Migration `Fase04_MaquinaEstadosConsulta`: editada à mão porque a scaffold
automática tentou "renomear" `STATUS_PAGAMENTO`→`TIPO_SERVICO` só por
coincidência de forma (dois enums sem relação nenhuma) — virou `AddColumn`/
`DropColumn` separados, com backfill de `STATUS` a partir de
`CANCELADA`/`FINALIZADA`/`STATUS_PAGAMENTO` antes de dropá-los.
**Próximos passos:** Fase 5 — pagamento simulado + comissão por plano: `Pagamento`
ganha `Simulado`/`PercentualComissao`/`ValorComissao`/`ValorRepasse`/
`DescontoFidelidadeCalculado`/`IncidenciaVetly`/`IncidenciaVeterinario`; nova
Strategy `IComissaoStrategy` (Básico 15% / Profissional 12% / Enterprise 10%)
selecionada por `Veterinario.Plano`; `PagamentoService.ProcessarSimuladoAsync`
cria o pagamento, calcula comissão/repasse e chama
`consulta.ConfirmarPagamento(agora)` (método já existe desde a Fase 4); endpoint
`POST /api/pagamentos/simular`.
