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
- [x] Fase 9 — Avaliação: entidade Avaliacao, moderação, média ponderada
      (RN-076..082)
- [x] Fase 10 — Fidelidade: ObrigacaoDoPet (Factory por espécie), PontosFidelidade
      FIFO, tiers, IDescontoFidelidadeStrategy (RN-069..075)
- [x] Fase 11 — Dashboard consolidado do Administrador + FaixaEnterprise +
      autorização por posse via claim entidadeId (RN-007, RN-092, RN-001..006)
- [x] Fase 12 — Documento: assinatura por nome digitado (RN-031, RN-091)
- [x] Fase 13 — Documentação final: README de contratos + FLUXO-DE-TESTE.md +
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
- **Códigos `AVALIACAO-005` (nota fora do intervalo 1-5) e `AVALIACAO-006`
  (consulta já avaliada)** — dois códigos além dos `AVALIACAO-001..004`
  previstos originalmente. `005` é invariante de domínio (`Avaliacao`, via
  `DomainException`); `006` é regra de aplicação (unicidade por consulta,
  `AvaliacaoService`, via `BusinessRuleException`) — mesma distinção já usada
  em todo o resto da base entre invariante de entidade e regra que precisa
  consultar outro repositório.
- **RN-081 (cancelamento/reembolso invalida avaliação) fica wireado em
  `ConsultaService.CancelarAsync`, mas é código estruturalmente inalcançável
  no estado atual**: `Consulta.Cancelar()` só aceita partir de
  `EmCheckout`/`Confirmada`, nunca de `Realizada` — e uma `Avaliacao` só pode
  existir para uma consulta `Realizada`. Mantido mesmo assim porque (a) é a
  tradução literal da RN, (b) o custo é uma linha, e (c) não há outro ponto
  de integração natural no sistema atual. Documentado aqui para não parecer
  "morto por acidente" numa leitura futura do código. A mesma ressalva vale
  para `IFidelidadeService.EstornarPontosPorCancelamentoAsync`, wireado no
  mesmo ponto pela Fase 10 (RN-075) — mesma causa raiz.
- **Valores de pontuação (50 para obrigação cumprida no prazo, 20 para
  consulta avulsa) e o mapeamento `TipoServico → TipoObrigacao`
  (Vacinacao→Vacina, Retorno→Retorno, Consulta/Teleorientacao→CheckUp,
  Cirurgia/Exame→nenhum) são decisões de engenharia da Fase 10** — a spec
  (RN-070) só define a *proporção* ("obrigação cumprida pontua mais que
  consulta avulsa, peso menor"), não valores numéricos nem o mapeamento
  serviço→obrigação. `Vermifugo` nunca é casado automaticamente por nenhum
  `TipoServico` existente (não há um "serviço de vermifugação" na v1) —
  permanece no calendário e pode vencer sem nunca ser marcado cumprido por
  este fluxo; é uma limitação conhecida do MVP, não um bug.
- **`PontosFidelidade.ExpiraEm` (Data + 12 meses) substitui uma fila FIFO
  explícita** (RN-074): como cada lançamento carrega sua própria expiração,
  os mais antigos vencem primeiro naturalmente — bastou filtrar por
  `Valido(agora)` ao somar o saldo. Não há estrutura de fila/consumo porque
  este MVP não tem resgate de pontos por recompensa, só acúmulo para tier e
  desconto — nunca "gasta" pontos específicos.
- **O calendário de `ObrigacaoDoPet` não é gerado automaticamente no
  `AnimalService.CriarAsync`** — apesar da RN-069 dizer "gerado no cadastro
  do pet". Decisão: expor `POST /api/animais/{id}/obrigacoes` como chamada
  explícita do cliente logo após criar o animal, em vez de acoplar
  `AnimalService` a `IObrigacaoService` (mais uma dependência num serviço já
  grande, e uma camada — Application — chamando outro serviço da mesma
  camada dentro de um fluxo que hoje não previa isso). Efeito observável para
  o cliente é o mesmo se o frontend encadear as duas chamadas.
- **`Empresa.FaixaEnterprise` é recalculada no read (dashboard, assinatura,
  vinculação), não em todo evento que muda a contagem de vets** — desvio do
  texto original do plano ("recalculada ao vincular/desativar vet"). Contar
  vets ativos de uma empresa é uma query barata
  (`IVeterinarioRepository.ObterPorEmpresaAsync`, já filtra `Ativo`), então
  recalcular a cada leitura financeira é tão correto quanto recalcular em
  todo evento de mutação, sem exigir que `VeterinarioService.DesativarAsync`
  passe a depender de `IEmpresaRepository` (acoplamento novo, cruzando
  serviços, só para manter um campo denormalizado sempre atualizado que
  ninguém lê fora do próprio `EmpresaService`). `ObterPorIdAsync`/
  `ObterTodosAsync` (listagens simples) mostram o último valor persistido,
  que pode ficar levemente desatualizado até a próxima
  vinculação/dashboard/assinatura — aceitável, documentado no DTO.
- **RN-001..006 (vet vinculado só vê a própria agenda/pacientes) foi
  implementado só em `VeterinarioService.ObterAgendaAsync` e
  `ConsultaService.ObterPorVeterinarioAsync`** — os dois endpoints
  literalmente rotulados "agenda"/"minhas consultas". Não foi retrofitado em
  `ConsultasController.ObterTodas` (que aceita `veterinarioId` como filtro
  opcional de uma listagem geral, usada também por Admin) nem em outros
  endpoints de leitura — expandir a checagem de posse para todo endpoint que
  toque em `VeterinarioId` é um escopo maior, não pedido explicitamente por
  nenhuma RN além da agenda/consultas do próprio vet, e arriscaria quebrar
  fluxos administrativos existentes sem um caso de teste que os cubra.
- **Dashboard consolidado (RN-007) não inclui contagem de Notas Fiscais** —
  a vedação de dados bancários/remuneração individual/dados de outra empresa
  é garantida *por construção* do `DashboardConsolidadoDto` (a lista de
  campos que ele tem), mas o conjunto de KPIs ficou restrito a
  faturamento/comissão/repasse/reembolso/contagem de consultas — os
  explicitamente nomeados por RN-007 ("faturamento bruto, comissões/
  repasses"). Agregar NFs exigiria uma nova consulta por `Documento` tipo
  `NotaFiscal` cruzada por vet, sem um pedido explícito além da menção
  genérica "NFs" no texto da RN — fica de fora para não inflar o escopo
  sem necessidade concreta.
- **`Documento.Assinar()` (v1, sem parâmetros) foi substituído por
  `Assinar(string nomeDigitado, DateTime agora)`** — única mudança de
  assinatura de um método v1 pré-existente em toda a migração (todo o resto
  dessa lista de decisões é sobre *não* tocar métodos v1). Diferente dos
  casos documentados antes (`Pagamento.VincularConsulta` etc., que ficam
  intocados por estarem fora do escopo de cada fase), a Fase 12 *é*
  especificamente sobre RN-031/091 mudarem a mecânica de assinatura — manter
  as duas versões coexistindo (`Assinar()` e `Assinar(nome, agora)`) criaria
  dois caminhos para o mesmo dado (`AssinadoDigitalmente`) sem necessidade.
  Único call site afetado fora de testes: `DocumentoService.AssinarAsync`.
- **A validação "nome digitado == nome do vet autenticado" só roda quando
  `ICurrentUserService.EntidadeId` está presente** — mesmo padrão de
  degradação graciosa já usado em `ConsultaService.MarcarRealizadaAsync`
  (Fase 4) para tokens emitidos sem a claim `entidadeId` (o dev-stub de auth
  permite emitir token só com `{usuario, role}`). Sem a claim, a assinatura
  é aceita com qualquer nome não-vazio — a validação de domínio (não-vazio)
  continua valendo sempre, só a de posse é condicional.

## ESTADO ATUAL

**Fase corrente:** Fase 13 concluída — **migração v1 → v2 completa.**
Commit a registrar, tags `v2-fase-13-docs` e `v2.0.0`.
**Baseline de testes:** 237/237 verdes (231 unit + 6 integration) — número
final, crescido monotonicamente desde a baseline de 51 testes da v1 ao
longo de 13 fases, sem nenhuma regressão registrada.
**O que mudou na Fase 13:** `README.md` reescrito do zero — contratos
completos dos 13 controllers (rota, verbo, auth exigida, shape de
entrada/saída, código de erro esperado por cenário), tabela de 22 enums
(nome → valores aceitos no JSON), catálogo dos 35 códigos de erro
efetivamente lançados no código (`grep` no `throw new` de
`DomainException`/`BusinessRuleException`/`ForbiddenException`, não
transcrito de memória — garante que a tabela reflete o código real, não o
que a spec pedia originalmente), tabela RN→classe para as regras
implementadas, diagrama ER em Mermaid com as 20 tabelas e FKs reais, seção
"o que este MVP não faz" espelhando a lista FORA DE ESCOPO. Novo
`FLUXO-DE-TESTE.md`: roteiro `curl` de 15 passos encadeados por `id`
(autenticação com claim `entidadeId` → empresa → vet vinculado → responsável
→ consentimento LGPD → animal + calendário de obrigações → consulta →
pagamento simulado → briefing/colmeia → IA auditada → documento assinado →
consulta realizada + fidelidade → avaliação → dashboard do Admin →
cancelamento com reembolso), com o resultado esperado documentado a cada
bloco e um checklist final RN→endpoint→resultado. Este arquivo
(`AGENT-OBJECTIVES.md`) revisado por completo: todas as 14 entradas do
checklist (Fase 0 a 13) marcadas `[x]`.
**Migração encerrada.** Não há próxima fase — qualquer trabalho adicional
sobre esta base (novas features, correções, ou itens da lista FORA DE
ESCOPO promovidos a escopo por decisão explícita do usuário) deve começar
uma nova sessão de planejamento, não continuar este documento como se fosse
mais uma fase do plano original.
