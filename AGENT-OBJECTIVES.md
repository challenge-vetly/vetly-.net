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

## ESTADO ATUAL

**Fase corrente:** Fase 10 concluída (commit a registrar, tag
`v2-fase-10-fidelidade`). Iniciando Fase 11.
**Baseline de testes:** 211/211 verdes (205 unit + 6 integration) — cresceu a
partir dos 173 da Fase 9 com `ObrigacaoDoPetTests` (6 casos, domínio puro),
`PontosFidelidadeTests` (6 casos, domínio puro), `ObrigacaoFactoryTests` (6
casos), `ObrigacaoServiceTests` (5 casos, Moq), `FidelidadeServiceTests` (11
casos, Moq) e 5 novos casos em `ResponsavelTests` (limiares de tier).
**O que mudou na Fase 10:** novas entidades `ObrigacaoDoPet` (`AnimalId`,
`Tipo: TipoObrigacao`, `DataLimite`, `Status: StatusObrigacao`,
`ConsultaId?`, `DataCumprimento?`, com `MarcarCumprida`/`EstaNoPrazo`/
`EstaAtrasada`) e `PontosFidelidade` (`ResponsavelId`, `ConsultaId`,
`Origem: OrigemPontos`, `Pontos`, `Data`, `ExpiraEm = Data+12m`,
`Estornado`) — FIFO resolvido pela própria `ExpiraEm` de cada lançamento,
sem fila separada (ver "Decisões de fundação"). `Responsavel` ganha
`RecalcularFidelidade(saldoValido)` (Prata≥300, Ouro≥800 em 12m — RN-071).
`IObrigacaoFactory` (canina/felina/genérica — mesmo padrão de seleção do
`IDocumentoFactory`, genérica sempre aplicável como fallback) gera o
calendário via `POST /api/animais/{id}/obrigacoes` (não automático no
cadastro do animal — ver "Decisões de fundação"). Novo `ObrigacaoService`
(gerar calendário com trava `OBRIGACAO-002` contra duplicar, listar com
"atrasada" derivada). Novo `IDescontoFidelidadeStrategy` por tier (Bronze
0%, Prata 5%=3%+2%, Ouro 10%=6%+4% — RN-072). Novo `FidelidadeService`:
`PontuarConsultaRealizadaAsync` (casa `TipoServico` com uma
`ObrigacaoDoPet` pendente no prazo — se achar, cumpre e dá pontos cheios;
senão, pontua como avulsa com peso menor — RN-070), sempre recalculando o
tier; `EstornarPontosPorCancelamentoAsync` (RN-075); `CalcularDescontoAsync`
(zera se `Responsavel.BloqueadoDescontosAte` ativo, mesmo com tier
elegível — RN-064 tem precedência sobre RN-072).
`ConsultaService.MarcarRealizadaAsync` chama `PontuarConsultaRealizadaAsync`
após marcar realizada; `CancelarAsync` chama
`EstornarPontosPorCancelamentoAsync` (mesma ressalva de alcançabilidade do
RN-081 na Fase 9). `PagamentoService.ProcessarSimuladoAsync` chama
`CalcularDescontoAsync` e grava o resultado via
`Pagamento.RegistrarDescontoFidelidade` (campos
`DescontoFidelidadeCalculado`/`IncidenciaVetly`/`IncidenciaVeterinario`, já
existentes e zerados desde a Fase 5) — `SimularPagamentoResponseDto` passa a
expor os três campos. Endpoints novos: `POST/GET
/api/animais/{id}/obrigacoes`, `GET /api/responsaveis/{id}/fidelidade`,
`GET /api/responsaveis/{id}/fidelidade/extrato`, `GET
/api/consultas/{id}/desconto-previsto?valorServico=X`. Migration
`Fase10_Fidelidade`: puramente aditiva (`CreateTable` para as duas tabelas
novas, sem rename — scaffold correto desta vez, incluindo `ESTORNADO` como
`bool` de verdade).
**Próximos passos:** Fase 11 — financeiro consolidado do Administrador +
faixas Enterprise: `Empresa` ganha `FaixaEnterprise` calculada pelo nº de
vets ativos (R$599 até 5, R$999 até 10, R$1.699 até 20, +R$70/vet acima de
20 — RN-092), recalculada ao vincular/desativar vet.
`EmpresaService.ObterDashboardConsolidadoAsync` agrega
faturamento/comissões/repasses/reembolsos — DTO sem nenhum campo de dados
bancários pessoais/outra empresa por construção (RN-007, não por filtro).
Autorização por posse via `ICurrentUserService.EntidadeId` (decisão de
fundação da Fase 0, ainda não exercitada em nenhum endpoint até aqui): vet
vinculado só acessa a própria agenda/pacientes; Admin só acessa `EmpresaId`
que bate com sua claim — tentativa cruzada ⇒ `ForbiddenException`
`ACESSO-002`. Endpoints `GET /api/empresas/{id}/dashboard`,
`GET /api/empresas/{id}/assinatura`.
