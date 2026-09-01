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
| RN-092 | Notificação é gravada antes de enviada e sobrevive ao push perdido; token recusado como inválido desativa o dispositivo, falha de provedor não | `Notificacao` + `NotificacaoService` + `IPushAdapter` |
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
