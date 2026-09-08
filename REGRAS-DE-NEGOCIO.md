# Vetly — Catálogo de Regras de Negócio

> Documento companheiro do [README principal](README.md). O README descreve **o que a API
> faz e como operá-la**; este arquivo descreve **por que ela se comporta como se comporta**.
> A separação é deliberada: quem chega para integrar precisa de rotas e payloads, e quem
> chega para alterar o comportamento precisa da regra e do lugar onde ela vive.

---

## Como ler esta tabela

A numeração segue o documento técnico oficial do produto (`vetly-tech.md`, **RN-001 a
RN-107**). Versões anteriores da documentação usavam uma numeração própria que colidia
com códigos diferentes do documento técnico — o de-para foi aplicado ao código, às
exceções lançadas em tempo de execução e a esta tabela, de modo que o código que aparece
no corpo de um erro HTTP é o mesmo que aparece aqui e o mesmo que aparece no documento
de produto.

Isso tem uma consequência prática que vale explicitar. Quando a API responde

```json
{
  "title": "Regra de negócio violada",
  "status": 422,
  "detail": "Este horario acabou de ser reservado por outra pessoa.",
  "codigo": "RN-035",
  "correlationId": "8c2f1a94b7d3e5f60112a3b4c5d6e7f8"
}
```

o campo `codigo` é uma chave desta tabela. Quem recebe o erro consegue ir direto da
resposta HTTP à regra que a produziu e ao arquivo que a implementa, sem intermediários.
O mesmo código também é publicado como métrica — `vetly_regras_violadas_total{codigo="RN-035"}`
— o que permite ver, em série temporal, quais regras estão sendo tocadas com que
frequência (ver a seção de observabilidade do README).

Códigos que **não** começam com `RN-` (`CONSULTA-001`, `INTERNACAO-002`, `PAGAMENTO-001`,
`AUTH-001`…) são invariantes de implementação, não regras do documento de produto: guardas
de estado que impedem uma operação sem sentido — cancelar o que já foi cancelado, registrar
procedimento em internação encerrada, calcular split sem consulta vinculada. Estão aqui
porque o cliente as recebe do mesmo jeito e precisa saber o que significam.

Cada linha aponta a **implementação** — a classe e, quando útil, o método onde a decisão
mora. Não é decoração: é o contrato de manutenção deste projeto. Uma regra que não aponta
para um lugar específico do código é uma regra que ninguém consegue verificar, e uma que
aponta para o lugar errado é pior do que nenhuma.

---

## Tabela

| Código | Descrição | Implementação |
|---|---|---|
| RN-006 | Consulta só pode ser agendada se o pagamento estiver com status Confirmado | `ConsultaService.AgendarAsync` |
| RN-022/RN-025 | Desativação de veterinário encerra o acesso e retorna agendamentos futuros ao chamador | `VeterinarioService.DesativarAsync` |
| RN-004 | Sem horário disponível, o Responsável entra na lista de espera do veterinário | `ListaEsperaService` |
| RN-037 | Vaga liberada é oferecida ao primeiro da fila com prioridade de 15 min; vencida, passa ao próximo | `ItemListaEspera` + `PromoverProximoAsync` |
| RN-026 | Endereço persistido no próprio registro, com latitude/longitude **derivadas dele** pela geocodificação — o payload do cliente é ignorado | `Endereco` + `IGeocodificacaoAdapter` |
| RN-033/RN-057 | Nota só é pública a partir de 3 avaliações; `PUBLICADO_EM` ancora o selo "Novo na Vetly" por 30 dias | `Veterinario.TemNotaPublica` + `PublicarNoMatching` |
| RN-106 | Métricas agregadas com denominadores explícitos; a taxa de aprovação sem correção mede se a IA ajuda, e o prontuário manual fica fora do denominador | `AnalyticsService` |
| RN-025 | Consulta de vet indisponível é redistribuída preservando pagamento e animal, com o horário novo travado antes da troca e o antigo liberado; o Responsável é avisado | `Consulta.Redistribuir` + `RedistribuicaoService` |
| RN-070/RN-072 | O consolidado verifica explicitamente que comissão + repasse + desconto fecha o bruto, e agrupa o repasse por destinatário pela maior pendência | `FinanceiroService.ObterConsolidadoAsync` |
| RN-071 | A liquidação registra um pagamento feito fora da plataforma, exige referência e ignora o que já estava liquidado; só cobrança confirmada entra | `FinanceiroService.LiquidarAsync` + `Pagamento.Liquidar` |
| RN-105 | O painel é sempre do próprio veterinário e destaca só o que trava dinheiro ou documento; avaliação sem resposta não conta como pendência bloqueante | `DashboardService.ObterDoVeterinarioAsync` |
| RN-105/RN-106 | O painel da unidade traz a agenda de todos os vinculados e os indicadores operacionais, com a empresa derivada do vínculo do próprio Admin — sem id na rota, porque com ele qualquer Admin leria o painel de qualquer clínica | `DashboardService.ObterDaUnidadeAsync` |
| RN-072 | Conta de repasse do prestador (banco, agência, conta, CPF/CNPJ do titular e chave Pix) embutida no registro, substituída inteira e lida só pelo titular, com conta e chave mascaradas | `DadosDeRepasse` + `VeterinarioService.DefinirDadosDeRepasseAsync` + `EmpresaService` |
| RN-072 | O consolidado diz **se** o destinatário tem conta de repasse, nunca qual — falso com repasse pendente é a linha que trava o fechamento | `FinanceiroService.ResolverDestinatarioAsync` |
| RN-092 | Notificação é gravada antes de enviada e sobrevive ao push perdido; token recusado como inválido desativa o dispositivo, falha de provedor não | `Notificacao` + `NotificacaoService` + `IPushAdapter` |
| RN-007/RN-092 | O agendamento só fecha com o aviso de confirmação, disparado no **webhook** — o estado autoritativo — e com data, horário e profissional no corpo | `PagamentoService.AvisarAgendamentoConfirmadoAsync` |
| RN-037/RN-092 | Vaga aberta na lista de espera avisa o primeiro da fila, com o horário e o prazo de 15 min no corpo — o mesmo prazo que o domínio vai cobrar | `ListaEsperaService.AvisarVagaAbertaAsync` |
| RN-014/RN-092 | O desfecho financeiro do cancelamento vira aviso, inclusive quando o reembolso é zero; quem cancelou muda o tipo — `ReembolsoConfirmado` para o Responsável, `CancelamentoPeloPrestador` para a operação | `ConsultaService.AvisarReembolsoAsync` |
| RN-016/RN-092 | Crédito de pontos avisa, e a subida de tier é anunciada só quando ocorre — o tier é recalculado depois do crédito e comparado com o de antes | `FidelidadeService.AvisarCreditoAsync` |
| RN-025/RN-092 | Redistribuição chega como `CancelamentoPeloPrestador`, e não como confirmação de agendamento: o app agrupa a caixa de entrada por tipo | `RedistribuicaoService.AvisarResponsavelAsync` |
| RN-094 | A régua nasce com o assunto da obrigação que a abriu, e não fixo em `Vacina`; antiparasitário cai em vermífugo e exame em check-up, porque a régua tem cinco assuntos e a obrigação tem sete | `AvisarObrigacoesVencendo.LembreteEquivalente` |
| RN-095 | O alerta de Responsável não responsivo aparece no painel do profissional e no da unidade, restrito aos animais que cada um atendeu | `DashboardService.MontarAlertasDaReguaAsync` + `ILembreteRepository.ObterEscaladosParaClinicaAsync` |
| RN-094/RN-095 | Régua diária transforma obrigação vencendo em um aviso por animal, com intervalo mínimo de 7 dias, e cria o lembrete que aciona a clínica após 3 tentativas | `AvisarObrigacoesVencendo` + `LembreteAgendado` |
| RN-055 | Só o Responsável atendido avalia, uma vez por consulta e em até **14 dias**; índice único garante a invariante sob concorrência | `Avaliacao` + `AvaliacaoService` |
| RN-059 | Cancelamento invalida a avaliação — sai do cálculo da nota, mas a linha fica com o motivo | `Avaliacao.Invalidar` + `AvaliacaoService.InvalidarPorCancelamentoAsync` |
| RN-057 | Reputação recalculada a partir das avaliações; abaixo de 3 a nota não é pública nem entra no score, e comentário moderado não tira a nota da média | `AvaliacaoService.RecalcularReputacaoAsync` + `Veterinario.TemNotaPublica` |
| RN-047 | Serviço pago rende 1 ponto por real; obrigação cumprida **no prazo** rende 50 pontos fixos — cumprir atrasado não credita | `MovimentoDePontos.PorServicoPago` / `PorObrigacaoCumprida` |
| RN-048 | Tier por acúmulo de 12 meses (Bronze/Prata/Ouro) com multiplicador 1,0/1,25/1,5 aplicado no crédito; o tier conta o creditado, não o saldo | `RegrasDeFidelidade.TierPara` |
| RN-049 | 100 pontos = R$ 3,00, arredondado a favor do programa nos dois sentidos | `RegrasDeFidelidade.EmReais` / `PontosPara` |
| RN-050 | Crédito é lote com saldo próprio; resgate consome em FIFO e a expiração baixa só o que sobrou | `MovimentoDePontos.Consumir` + `FidelidadeService.ConsumirFifoAsync` |
| RN-051 | O custo do desconto é dividido por faixa (100/0 · 60/40 · 30/70): a parte da Vetly sai da comissão, a do prestador sai do repasse, e as três parcelas fecham o bruto | `RegrasDeFidelidade.Dividir` + `Pagamento.AplicarDesconto` |
| RN-052 | Cancelamento estorna os pontos da consulta, tirando só o que ainda não foi gasto | `FidelidadeService.EstornarPorConsultaAsync` |
| RN-053/RN-054 | Cupom com QR e 30 dias; vencido, os pontos não voltam; vale para uma transação | `CupomResgate` |
| RN-036 | Pré-sintomas em texto guiado + mídias, aceitos só antes do atendimento; lista vazia grava o sentinela `";"`, porque no Oracle string vazia é NULL | `Consulta.RegistrarPreSintomas` |
| RN-041/RN-042 | A simulação de cancelamento reusa a mesma Strategy do cancelamento e não deixa rastro — mostrar um valor e cobrar outro é o que a regra proíbe | `ConsultaService.SimularCancelamentoAsync` |
| RN-013/RN-043 | Remarcar transfere o pagamento e incrementa o contador da consulta, limitado a 2; esgotado, resta cancelar | `Consulta.RemarcarPara` |
| RN-044 | No-show é registrado por quem esperava — nunca pelo próprio Responsável — e não gera reembolso | `ConsultaService.RegistrarNoShowAsync` |
| RN-045 | Obrigação de cuidado guarda periodicidade e se reagenda sozinha ao ser cumprida, contando a partir do cumprimento; `Vencendo` avisa 30 dias antes | `ObrigacaoPet` + `ObrigacaoService` |
| RN-046 | Obrigações derivadas da carteira de vacinação, uma por tipo, a partir da dose mais recente; derivar de novo não duplica | `ObrigacaoService.DerivarDaCarteiraAsync` |
| RN-090 | Colmeia: o Responsável (e só ele) autoriza um veterinário de fora a alcançar o histórico do animal, com escopo e prazo; concessão vigente duplicada devolve 409 | `AcessoColmeia` + `ColmeiaService` |
| RN-090 | Todo acesso pela colmeia — permitido ou negado — vai para uma trilha append-only que o Responsável consulta; revogar não apaga o que já foi acessado | `LogAcessoColmeia` + `ColmeiaRepository` |
| RN-105/RN-106 | Escopo por linha: o Responsável só alcança os próprios dados, o veterinário só os animais que atende, e o escopo vem do token — não de parâmetro do cliente | `IUsuarioAtual` + guardas em `AnimalService`, `ConsultaService`, `PagamentoService`, `TutorService` |
| RN-001/RN-002 | Busca lista clínicas e vets autônomos por proximidade e necessidade, ordenados por score | `BuscaService` |
| RN-029 | O animal é obrigatório na busca, e a ausência devolve 400 e não 404: `[Required]` sobre `Guid` não anulável nunca dispara, e sem a guarda o `Guid.Empty` chegava ao serviço como chave | `GuidObrigatorioAttribute` + `FiltroBuscaDto` |
| RN-027 | Distância entre a posição do Responsável e a coordenada do prestador; CEP é o fallback quando a localização é negada | `BuscaService.ResolverPosicaoAsync` |
| RN-028 | Raio de 10 km por padrão, expansível até 25 km | `BuscaService` |
| RN-029 | Espécie atendida é filtro **eliminatório** — matching clinicamente inválido não aparece | `BuscaService.Elegivel` |
| RN-030/RN-031 | Score 40/30/30 e desempate por nota → distância → disponibilidade em 48h | `BuscaService.CalcularScore` |
| RN-042 | Percentual de retenção do cancelamento parcial é configurado pela clínica no onboarding (padrão 30%) e lido no cancelamento | `Empresa.DefinirPoliticaRetencao` |
| RN-072 | Faixa Enterprise recalculada automaticamente ao cruzar o limite de vets vinculados | `Empresa.RecalcularFaixaEnterprise` |
| RN-003 | Com clínica, a consulta é atribuída ao profissional dono do horário escolhido; com autônomo, direto com ele | `ConsultaService.IniciarCheckoutAsync` |
| RN-034 | Agenda configurável (dias, horário, duração, intervalo) materializada em horários por 60 dias | `AgendaConfig` + `AgendaService` |
| RN-035 | Slot com lock de checkout de 10 min: `Livre → EmCheckout → Confirmado`; horário já reservado devolve 409 | `Slot` + `ConsultaService.IniciarCheckoutAsync` |
| RN-039/RN-040 | Atendimento remoto fora de escopo; `POST /api/consultas` é oficialmente a rota de emergência/balcão, marcada na origem da consulta | `ConsultaService` |
| RN-035/RN-038 | Estado da consulta em enum `StatusConsulta` (EmCheckout → Confirmada → Realizada / Cancelada / NoShow / Expirada), substituindo os três booleanos | `Consulta.Status` |
| RN-041 | Cancelamento com mais de 24h de antecedência = reembolso integral | `ReembolsoIntegralStrategy` |
| RN-041/RN-042 | Cancelamento entre 2h e 24h = reembolso parcial, com o percentual configurado pela clínica (padrão 30%) | `ReembolsoParcialStrategy` + `ConsultaService.CancelarAsync` |
| RN-041 | Cancelamento com menos de 2h = sem reembolso | `SemReembolsoStrategy` |
| RN-022/RN-024 | Vet desativado entra com role `VetDesativado` e é bloqueado em toda rota de negócio, mantendo só o que a RN-024 garante | `VetDesativadoFilter` + `AuthService` |
| RN-022 (§4.1) | Administrador **não** é cadastro à parte: é o veterinário que a empresa aponta em `Empresa.AdministradorId`, e a role `Admin` é derivada desse vínculo no login. Desativado vence administração — inverter a ordem deixaria um administrador desligado da unidade continuando a administrá-la | `AuthService.RoleDeVeterinarioAsync` |
| §4.1 | O primeiro administrador nasce da configuração, e só quando não há unidade nenhuma: criar empresa exige ser `Admin`, e ser `Admin` exige administrar empresa | `SemeadorDoAdministrador` |
| RN-024 | O extrato é a única rota de negócio que o vet desativado alcança, e não carrega dado de Responsável, de animal nem clínico — só o registro financeiro do próprio trabalho | `VeterinarioService.ObterExtratoAsync` + `[PermitidoAoVetDesativado]` |
| RN-060 | Sem consentimento de atendimento, as rotas de negócio do Responsável devolvem 422 — a base legal precede o tratamento | `ConsentimentoAtendimentoFilter` |
| RN-061/RN-062 | Consentimento granular por finalidade, com data de concessão e de revogação; revogar não apaga registro clínico já produzido | `Tutor.RegistrarConsentimento` + `TutorService` |
| RN-006 | A consulta só é confirmada com o pagamento, e a confirmação vem do **webhook**, nunca da resposta síncrona | `PagamentoService.ProcessarWebhookAsync` |
| RN-070 | Take rate por plano: Básico 15%, Profissional 12%, Enterprise 10% — a maior comissão pertence ao menor plano | `SplitBasicoStrategy`, `SplitProfissionalStrategy`, `SplitEnterpriseStrategy` |
| RN-072 | Repasse único: ao vet autônomo ou à clínica. Vet vinculado usa o plano da unidade, e a remuneração interna fica fora do escopo | `PagamentoService.ResolverPlanoEDestinatarioAsync` |
| RN-081 | Sugestão de dose exige peso do animal — `POST /api/ia/protocolo` com peso ausente/zero devolve 422, e o cadastro do pet passa a exigir `pesoKg` | `OllamaService.SugerirProtocoloAsync` + `AnimalService` |
| RN-008 | A consulta tem uma janela explícita: `iniciar` abre, `encerrar` fecha e marca a consulta como `Realizada`; iniciar ou encerrar duas vezes devolve 409 | `SessaoCaptura` + `CapturaService` |
| RN-009 | Áudio capturado em segmentos sequenciais, transcritos fora da requisição; reenvio da mesma sequência devolve 409, e falha em parte dos trechos gera rascunho parcial em vez de perder a consulta | `SegmentoAudio` + `TranscreverSegmentoHandler` |
| RN-079 | Fora da janela de captura a IA não captura áudio nem produz conteúdo clínico — trecho enviado com a janela fechada devolve 409 | `SessaoCaptura.JanelaAberta` |
| RN-085 | Captura e IA na consulta existem nos planos Profissional e Enterprise; no Básico a consulta inicia sem captura e o prontuário é manual | `CapturaService.PlanoTemCapturaAsync` |
| RN-080 | A IA estrutura a transcrição em prontuário fora da requisição; o rascunho guarda o texto de origem e o modelo, e transcrição parcial vira rascunho parcial com aviso | `OllamaService.EstruturarConsultaAsync` + `RascunhoService` |
| RN-082 | Decisão sobre o rascunho da IA em três caminhos (aprovar / corrigir / não aprovar), cada um com o que o torna auditável; não aprovar não valida o diagnóstico | `ProntuarioService.DecidirAsync` |
| RN-082 | Toda decisão vira registro append-only com o conteúdo final, quem decidiu e o modelo — o repositório não tem atualizar nem remover | `LogAuditoriaIa` + `AuditoriaIaRepository` |
| RN-085 | Prontuário manual fecha o atendimento quando não houve IA no caminho; com rascunho pendente devolve 409 | `ProntuarioService.RegistrarManualAsync` |
| RN-082 | Documentos só podem ser gerados após `consulta.DiagnosticoValidado = true` E pagamento confirmado | `DocumentoService.GerarAsync` |
| RN-083 | O conteúdo do documento é formatação do estado final aprovado, lido da trilha de auditoria; sem conteúdo aprovado, não se gera documento | `DocumentoService.ObterConteudoAprovadoAsync` + factories |
| RN-086 | O subtipo do atestado muda o texto do documento (óbito, saúde, vacinação), e não apenas o rótulo | `AtestadoFactory.Declaracao` |
| RN-090 | Documento gerado vira PDF no storage, com URL sempre temporária; publicar no board é passo separado, e receita só vai ao board assinada | `IGeradorDePdf` + `DocumentoService.PublicarAsync` |
| RN-010 | A decisão do veterinário em `validar-diagnostico` (`Aprovado`/`Corrigido`) **habilita** a emissão; ela não a executa. Cada documento é emitido por ato explícito do profissional, um por tipo — o sistema não gera o conjunto sozinho (ver a nota abaixo) | `ProntuarioService.DecidirAsync` habilita · `DocumentoService.GerarAsync` emite |
| RN-011 | A publicação no board é automática assim que o documento é gerado e assinado: publicar grava `PublicadoEm`, entra no histórico do animal e dispara a notificação `DocumentoPublicado` ao Responsável | `DocumentoService.PublicarAsync` + `TipoNotificacao.DocumentoPublicado` |
| RN-087 (C-04) | Finalizar exige que todo documento **já emitido** que precise de assinatura esteja assinado — receita e atestado; consulta que não prescreveu nada finaliza normalmente | `Documento.PendenteDeAssinatura` + `ConsultaService.FinalizarAsync` |
| RN-087 | Assinatura por adaptador: nome digitado conferido contra o registrado, carimbo no corpo do documento dizendo como foi assinado e o que não habilita | `IAssinaturaAdapter` + `AssinaturaAdapterNomeDigitado` |
| RN-088 | Correção cria nova versão do documento (original preservado com `VersaoOriginalId`) | `DocumentoService.CorrigirAsync` |
| RN-089 | Correção após 24h exige justificativa não vazia | `DocumentoService.CorrigirAsync` |
| RN-094 | Resposta do tutor encerra a régua de contato | `LembreteService.RegistrarRespostaAsync` |
| RN-095 | Após 3 tentativas sem resposta, `AlertaEnviadoClinica = true` | `LembreteService.ProcessarTentativaAsync` |
| RN-100 | Procedimentos acumulam `ValorTotalApurado`; alta retorna `saldo = total − caução` | `InternacaoService.RegistrarProcedimentosAsync` + `DarAltaAsync` |
| RN-107 | CRMV consultado no conselho regional via `ICrmvAdapter`; `Indisponivel` mantém o perfil pendente e fora do matching — nunca se aprova por omissão | `CrmvAdapterSimulado` + `VeterinarioService.RevalidarCrmvAsync` |
| RN-107 | CRMV validado por regex `^\d{4,6}-[A-Z]{2}$` + duplicidade; perfil nasce `PendenteValidacao` e só é publicado no matching com CRMV `Valido` (adaptador do conselho: C-05) | `VeterinarioService.CriarAsync` + `Veterinario.PublicarNoMatching` |
| CONSULTA-001 | Consulta já cancelada não pode ser cancelada novamente | `ConsultaService.CancelarAsync` |
| CONSULTA-002 | Pagamento da consulta não encontrado ao cancelar | `ConsultaService.CancelarAsync` |
| CONSULTA-003 | Não é possível validar diagnóstico de consulta cancelada | `ConsultaService.ValidarDiagnosticoAsync` |
| INTERNACAO-001 | Animal já possui internação ativa | `InternacaoService.AbrirAsync` |
| INTERNACAO-002 | Não é possível registrar procedimentos em internação encerrada | `InternacaoService.RegistrarProcedimentosAsync` |
| PAGAMENTO-001 | Split exige `ConsultaId` preenchido no pagamento | `PagamentoService.ProcessarSplitAsync` |
| TUTOR-001 | Tutor não encontrado | `TutorService` |
| LEMBRETE-001 | Lembrete não encontrado | `LembreteService` |

---

## Emissão de documentos: por que o veterinário escolhe (RN-010/RN-011)

RN-010 ("o sistema gera prontuário, atestado, receita e NF") e RN-011 ("uma automação
publica os documentos") leem como emissão automática ao encerrar a consulta. A
implementação exige que o veterinário chame
`POST /api/documentos/consulta/{id}?tipo={TipoDocumento}` para cada documento que decidir
emitir. A divergência é **deliberada**, e está registrada aqui para que quem auditar a
rastreabilidade não a trate como defeito.

**O gatilho é a decisão, não o encerramento.** `validar-diagnostico` com `Aprovado` ou
`Corrigido` fixa o estado final aprovado e destrava a emissão (RN-082). `NaoAprovado`
encerra o ciclo sem documento nenhum. Nada é emitido antes dessa decisão, e o que a
emissão faz depois é **formatar o estado final** — não inferir clínica nova (RN-083).

**A emissão de cada documento é ato do profissional, por tipo.** Atestado de saúde, de
óbito e de transporte são **atos privativos do médico veterinário**: emitir por default
transformaria um ato privativo em efeito colateral de encerrar a consulta — a plataforma
afirmaria, em nome de um profissional habilitado, algo que ele não escolheu afirmar. O
mesmo vale para a receita: nem toda consulta prescreve, e gerar receita sempre levaria o
veterinário a assinar documento vazio só para conseguir fechar o atendimento (é a mesma
razão do C-04 na RN-087).

**A publicação, essa sim, é automática.** Uma vez gerado e assinado, o documento vai ao
board do pet por `POST /api/documentos/{id}/publicar`, entra no histórico do animal e
dispara a notificação `DocumentoPublicado` ao Responsável. O que a RN-011 descreve
funciona; o que não existe é a automação que decide **quais** documentos existem.

**Fecho do ciclo.** Como não há job de geração declarando o ciclo terminado, quem o fecha
é `POST /api/consultas/{id}/finalizar`, o mesmo ato que a RN-087 já exige do profissional:
ele leva a sessão de captura a `Concluida` e devolve esse estado, que é o que tira o app
do polling.

---

## A matriz de canais (§6.2) e onde cada evento nasce

A matriz do documento de produto lista onze eventos que o Responsável tem de receber
in-app e por push. Ela é fácil de ler como decoração de produto e difícil de auditar
no código, porque nenhum lugar único a implementa — cada evento nasce no serviço que
produz o fato. A tabela abaixo existe para que a auditoria seja uma leitura, e não uma
busca.

| Evento (§6.2) | Tipo | Onde é disparado |
|---|---|---|
| Confirmação de agendamento | `ConsultaConfirmada` | `PagamentoService.AvisarAgendamentoConfirmadoAsync` (webhook) · `ConsultaService` (retorno) |
| Vaga aberta em lista de espera | `HorarioDisponivel` | `ListaEsperaService.PromoverProximoAsync` |
| Documentos do atendimento | `DocumentoPublicado` | `DocumentoService.PublicarAsync` · `ExameService` |
| Confirmação de reembolso | `ReembolsoConfirmado` | `ConsultaService.CancelarAsync` |
| Lembrete de vacina / vermífugo | `ObrigacaoVencendo` | `AvisarObrigacoesVencendo` + `AgendarTentativasDaRegua` |
| Lembrete de retorno | `ObrigacaoVencendo` | idem — o assunto vem do tipo da obrigação |
| Lembrete de medicação / recompra | `ObrigacaoVencendo` | idem |
| Check-up preventivo | `ObrigacaoVencendo` | idem |
| Pontos creditados / mudança de tier | `PontosCreditados` | `FidelidadeService` (crédito por consulta e por obrigação) |
| Promoções | `Promocao` | opt-in conferido em `NotificacaoService.CriarAsync` (RN-093) |
| Mudança decidida pelo prestador | `CancelamentoPeloPrestador` | `RedistribuicaoService` · `ConsultaService.CancelarAsync` |

Três tipos do enum **não** têm gatilho, e é deliberado registrar isso em vez de deixar
a ausência parecer esquecimento: `ConsultaProxima` e `AvaliacaoPendente` não estão na
matriz do produto — a avaliação é puxada pela rota `GET /api/avaliacoes/pendentes`, e
não empurrada —, e `PontosExpirando` depende de uma rotina de varredura de lotes a
vencer que a §6.2 não pede. Os três seguem no enum porque o app já os distingue por
ícone e agrupamento, e removê-los renumeraria valores já persistidos.

O agrupamento por tipo é o que dá sentido à distinção. `ReembolsoConfirmado` e
`CancelamentoPeloPrestador` descrevem o mesmo cancelamento e existem separados porque
respondem a perguntas diferentes: um diz o que aconteceu com o dinheiro de um ato que
o Responsável praticou; o outro diz que alguém desmarcou o atendimento dele. Mandar o
primeiro para quem não cancelou nada leria como confirmação de um ato próprio.

---

## Por que a conta de repasse não tem id na rota

`GET /api/veterinarios/me/dados-repasse` e o `PUT` correspondente não aceitam id de
veterinário, e isso não é economia de parâmetro. A §7.3 veda ao administrador da
unidade os "dados bancários pessoais" dos profissionais vinculados. Com um `Guid` na
rota, essa vedação dependeria de uma checagem no serviço — e passaria a existir
enquanto ninguém a removesse por engano. Sem o parâmetro, não há o que trocar.

A conta da **empresa** é outra coisa e mora em outra rota
(`/api/empresas/{id}/dados-repasse`, `ApenasAdmin`): é a conta do estabelecimento, que
é justamente quem recebe o repasse quando o vet é vinculado, e a remuneração interna
fica fora do escopo da plataforma.

A leitura devolve conta e chave Pix **mascaradas** nas duas rotas. Quem lê é o titular
conferindo o que cadastrou, e os últimos dígitos bastam; devolver o número inteiro
transformaria um token vazado, um log de resposta ou um print de tela em dado bancário
completo. O mascaramento mora no value object, e não em cada mapeamento — a decisão de
o que esconder é do domínio, não de quem lembrar de aplicá-la.

---

## O princípio por trás das regras

Três ideias organizam quase todas as decisões acima, e reconhecê-las ajuda a prever como
uma regra nova deveria se comportar.

**A primeira é que o prontuário pertence ao animal, não à clínica.** É a tese central do
produto — a "mente colmeia" — e ela é o que faz a Vetly ser um ativo insubstituível em vez
de um software de gestão a mais. Dela decorrem a colmeia por evento clínico (RN-090), a
permanência do histórico após o desligamento do profissional (RN-023/RN-024), o log de
acesso append-only e a recusa de qualquer caminho que permita a um estabelecimento
autoconceder acesso ao histórico de um animal que não atendeu.

**A segunda é que o servidor decide o que é do servidor.** Valor de serviço vem de
`Servico.Valor`, nunca do corpo da requisição; a identidade do Responsável vem da claim do
token, nunca de um parâmetro de rota; o plano que define a comissão é ato do Admin, nunca
do próprio profissional que seria beneficiado por baixá-lo. Toda vez que uma regra parecer
"chata" demais, quase sempre ela está impedindo que o cliente decida algo que o servidor
não pode delegar.

**A terceira é que o registro não se reescreve.** Documento corrigido gera nova versão com
a original preservada (RN-088); avaliação de consulta cancelada é invalidada, não apagada
(RN-059); moderação esconde o comentário e mantém a nota; a trilha de decisões sobre a IA
é append-only por construção — o repositório sequer expõe atualizar ou remover. Sistemas
clínicos e financeiros são auditados, e a única forma de sustentar uma auditoria é nunca
ter perdido a história.

Quando surgir a dúvida sobre como uma regra nova deveria se comportar, essas três costumam
responder antes de qualquer discussão.
