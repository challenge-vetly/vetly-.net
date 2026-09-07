# Documento de Produto — Vetly

## 1. Identidade do Produto

**Vetly** é a plataforma intermediária entre Responsáveis de pets e veterinários. A Vetly não presta serviço veterinário: ela **conecta, relaciona e retém** Responsáveis e veterinários, organiza o agendamento, cobra, reparte e guarda o histórico.

**Tese central:** o produto fideliza o cliente **com o app, não com veterinários ou clínicas específicas**. O compromisso da Vetly é com o Responsável. O prontuário pertence ao **animal** ("mente colmeia"), não à clínica — logo qualquer vet da rede atende com contexto completo, e a diferenciação entre vets se desloca para **preço, qualidade, infraestrutura e equipamentos**. Esse nivelamento é um efeito estratégico desejado: estimula investimento e competição saudável, e transforma a Vetly no ativo insubstituível (os dados e a conveniência ficam na plataforma).

O Responsável opera **inteiramente dentro do app Vetly**. Toda a operação — busca, agendamento, pagamento, documentos, avaliação, fidelidade e notificações — acontece no app.

**Proposta de valor por lado:**

| Lado | Proposta de valor |
|---|---|
| Responsável | Encontra vet/clínica por geolocalização e necessidade, agenda com pré-sintomas, acompanha o pet por avatar digital, acumula pontos de fidelidade e mantém o histórico único do animal no app. |
| Veterinário autônomo | Perfil no matching, agenda própria, repasse direto, redução de fricção clínica pela IA que monta os documentos a partir da fala da consulta. |
| Empresa / Clínica | Gestão multiprofissional, agenda e financeiro consolidados da unidade, matching para captação de demanda e a mesma redução de fricção clínica por IA. |

O foco comercial da Vetly é trabalhar com **clínicas veterinárias**.

### 1.1 Escopo do MVP

| Área | No MVP | Alvo (produção) |
|---|---|---|
| Matching por geolocalização | Busca de clínicas e vets autônomos por localização e tipo de serviço | Ranking por reputação, distância e disponibilidade em tempo real |
| App do Responsável | Onboarding, cadastro de pets, board do pet, busca, agendamento com pré-sintomas, carteira, avaliação, fidelidade, marketplace, notificações | Mesmo escopo com dados e liquidação reais |
| Avatar digital do pet | Totalmente mocado, apenas cachorro | Multi-espécie, sinais dinâmicos a partir de dados reais do calendário de obrigações |
| Fidelidade / recompensas | Acúmulo e tier reais; desconto calculado e exibido; cupom por QR code; sem abatimento real (pagamento simulado) | Abatimento real integrado ao checkout |
| IA na consulta | Captura da fala do vet e geração de documentos (planos Profissional e Enterprise) | Mesma função com automação completa de publicação |
| Receita com assinatura | Nome digitado | Assinatura digital com certificado vinculado ao CRMV |
| NF + split | Registrado, não liquidado | Liquidação via gateway |
| Avaliação | Nota geral obrigatória, 1 por consulta realizada | Critérios múltiplos (qualidade, disponibilidade, distância) |
| Marketplace | Mocado no front-end; taxa de listagem por categoria definida, mas não cobrada | Catálogo transacional com parceiros reais e cobrança efetiva da taxa |
| Atendimento remoto | Fora do escopo desta fase | Sala de atendimento integrada ao app |
| Integrações externas | Fora do escopo desta fase | A definir |

## 2. Personas

1. **Responsável** (persona de primeira classe). Usa o app: onboarding, cadastro de pets, board do pet, busca, agendamento com pré-sintomas, carteira, avaliação, fidelidade, marketplace, notificações.
2. **Veterinário autônomo.** PJ independente; agenda própria, repasse direto.
3. **Empresa / Administrador.** Clínica/hospital/pet shop; gerencia vets vinculados, agendas, serviços e o financeiro consolidado da unidade (vedações em §7.3).

## 3. App do Responsável

### 3.1 Onboarding e Cadastro do Pet

O Responsável cria sua conta no app e registra o consentimento LGPD (§7.1) antes de qualquer outra interação. No cadastro do pet informa: nome, espécie, raça, sexo, nascimento/idade, **peso**, foto, castrado, condições pré-existentes, alergias e carteira de vacinação. Um Responsável tem N pets. O cadastro gera o **calendário de obrigações** do animal, que alimenta o board do pet, o avatar digital (§3.3) e a comunicação proativa (§6).

### 3.2 Busca por Geolocalização

O app captura a geolocalização do Responsável e apresenta **clínicas** e **veterinários autônomos** que atendem a necessidade do momento — banho, tosa, consulta de emergência, consulta de rotina, entre outros. Nas clínicas, a consulta é marcada com o profissional que a clínica designar; com o vet autônomo, diretamente com ele. Os resultados exibem as avaliações (§3.6) para o Responsável escolher por qualidade de atendimento, disponibilidade e distância.

### 3.3 Board do Pet e Avatar Digital

O board do pet reúne o histórico do animal, o calendário de obrigações e o **avatar digital**. O avatar acompanha ao vivo as necessidades do animal: quando uma vacina passa do prazo, o avatar aparece com a carinha doente; quando o prazo de higienização passa, o pelo aparece maior e o avatar reclama dos pelos, e assim por diante. No MVP o avatar é **totalmente mocado** e tratado apenas para **cachorro**.

### 3.4 Agendamento com Pré-Sintomas

O Responsável agenda pelo app, escolhendo o pet, o tipo de serviço, a clínica ou vet e o horário disponível. Durante o agendamento, o Responsável informa **pré-sintomas** e informações relevantes sobre o animal, que compõem o **briefing pré-consulta** entregue ao veterinário (§5.1). O agendamento é confirmado após o pagamento; NF e split são **registrados, não liquidados** no MVP (§7).

Quando não há horário disponível para o serviço ou vet solicitado, o app apresenta três opções: entrar em lista de espera para aquele vet, ver horários com outros vets da mesma clínica ou região, ou escolher uma data futura. Ao abrir vaga na lista de espera, o Responsável recebe notificação com confirmação em um toque.

### 3.5 Carteira

A carteira concentra os pagamentos, recibos, NFs e o status de reembolsos do Responsável dentro do app. O reembolso segue a política configurada pela clínica: cancelamentos com mais de 24 horas de antecedência geram reembolso integral; entre 24 horas e 2 horas, reembolso parcial com percentual de retenção configurável pela clínica no onboarding; com menos de 2 horas ou no ato, sem reembolso. O Responsável é informado da política no momento do agendamento.

### 3.6 Avaliação

O Responsável avalia a consulta realizada. No MVP, só a **nota geral** é obrigatória e há **1 avaliação por consulta realizada**. As avaliações alimentam a busca (§3.2), permitindo escolha por qualidade de atendimento, disponibilidade e distância.

### 3.7 Fidelidade e Recompensas

Cumprir as obrigações do pet no prazo gera **pontos** e faz o Responsável subir de **tier** (Bronze / Prata / Ouro). Acúmulo e tier são **reais**.

**Como ganha pontos:** serviços pagos na plataforma creditam **1 ponto por R$ 1 gasto**; cada obrigação do calendário do pet cumprida no prazo (vacina, vermífugo, retorno, check-up, higienização) credita **50 pontos fixos**, independentemente do valor. O tier aplica um **multiplicador de ganho** crescente sobre os dois. Pontuar por obrigação cumprida, e não só por gasto, é o que alinha o programa à tese do produto: recompensa cuidado preventivo, não consumo.

| Tier | Faixa de acúmulo (pontos em 12 meses) | Multiplicador de ganho |
|---|---|---|
| Bronze | 0 – 999 | 1,0× |
| Prata | 1.000 – 2.999 | 1,25× |
| Ouro | 3.000+ | 1,5× |

**Conversão:** a cada 100 pontos = R$ 3 em desconto. Os pontos podem ser trocados por descontos em novas consultas, rações, remédios, banhos, entre outros. O **desconto em reais é calculado e exibido** (quanto o Responsável economizaria e como a incidência se divide Vetly × vet), **sem abatimento real** porque o pagamento é simulado no MVP.

**Expiração:** os pontos expiram em 12 meses a partir do crédito, consumidos do mais antigo para o mais novo. O cupom gerado no resgate vale 30 dias; expirado, os pontos não retornam ao saldo.

**Financiamento do desconto (Vetly × vet):** o desconto resgatado por pontos não é um custo extra cobrado à parte — ele é absorvido dentro do próprio split da transação (§9), no mesmo modelo em que outras plataformas de cupom financiam suas promoções: parte sai da comissão da Vetly, parte sai do repasse do vet/estabelecimento, e a proporção muda conforme o valor resgatado.

| Valor resgatado no atendimento | Parte absorvida pela Vetly (reduz sua comissão) | Parte absorvida pelo vet/estabelecimento (reduz o repasse) |
|---|---|---|
| Até R$ 10 | 100% | 0% |
| R$ 10,01 a R$ 30 | 60% | 40% |
| Acima de R$ 30 | 30% | 70% |

Descontos pequenos — típicos de quem está começando a acumular pontos — saem integralmente da comissão da Vetly, sem impacto no repasse do vet. À medida que o Responsável acumula mais pontos e resgata valores maiores, o repasse do vet/estabelecimento passa a absorver a maior parte do desconto. No MVP essa divisão é apenas calculada e exibida, sem abatimento real.

### 3.8 Marketplace

O marketplace lista os produtos e serviços resgatáveis por pontos (rações, remédios, banhos, consultas, entre outros). Para um produto aparecer no marketplace, a empresa fornecedora (fabricante, distribuidora, clínica ou pet shop parceiro) paga uma **taxa de listagem por categoria**, no mesmo modelo usado por outros marketplaces (Mercado Livre, Magalu, Amazon), em que o valor varia conforme a categoria do item, não é uma comissão sobre a venda:

| Categoria | Taxa de listagem mensal por item |
|---|---|
| Alimentação e rações | R$ 150 |
| Medicamentos e farmácia | R$ 250 |
| Higiene, banho e bem-estar | R$ 100 |

Serviços clínicos oferecidos pelo próprio vet/clínica (consultas, exames, procedimentos) não pagam taxa de listagem — já são monetizados pela assinatura do plano e pelo split (§9).

A taxa de listagem é uma linha de receita da Vetly (§9) e não se confunde com o financiamento do desconto (§3.7), que é resolvido no split da transação.

Ao trocar pontos por um item, o Responsável gera um **cupom em QR code** que leva ao estabelecimento e resgata o desconto do item escolhido. No MVP, o marketplace inteiro é **mocado no front-end**: não há empresas reais cadastradas nem cobrança efetiva da taxa de listagem, o pagamento é simulado e o resgate por QR code não movimenta valores reais. A taxa de listagem existe apenas como regra definida para a fase de produção.

### 3.9 Notificações

O Centro de Notificações do Responsável está descrito em §6.

## 4. Onboarding do Vet / Empresa

### 4.1 Cadastro, Estrutura de Acesso e Repasse

A plataforma atende o **veterinário autônomo** (PJ independente, agenda própria, atende múltiplos Responsáveis) e a **empresa** (clínica, hospital, pet shop ou estabelecimento com múltiplos profissionais). Na empresa há um **administrador** que cadastra, edita e desativa os veterinários vinculados. Cada veterinário vinculado enxerga apenas sua própria agenda e seus próprios pacientes; o administrador tem visibilidade total sobre profissionais, agendas e atendimentos da unidade.

O veterinário preenche seus dados profissionais: CRMV, UF de atuação, especialidades, espécies atendidas e titulação acadêmica. O sistema valida automaticamente o CRMV junto ao conselho regional antes de publicar o perfil; perfis com CRMV inválido ou suspenso não são publicados e o profissional é notificado. Em seguida configura a agenda (dias e horários, duração média e intervalo) e os serviços oferecidos (consulta, cirurgia, exame, vacinação, entre outros), informando valor e se aceita plano de saúde pet.

O veterinário cadastra dados bancários (banco, agência, conta, CPF ou CNPJ do titular) e chave Pix. O **split** é configurado no onboarding conforme o plano (§9): para o autônomo, entre o profissional e a plataforma; para a empresa, entre o estabelecimento e a plataforma. A remuneração dos vinculados é relação interna da empresa e está fora do escopo da plataforma. **No MVP não há gateway de pagamento integrado**: todo o pagamento é simulado, e NF e split são **registrados, não liquidados** (§7).

O **veterinário vinculado** gerencia apenas a própria agenda, acessa apenas os prontuários dos animais que atendeu ou que estão agendados para ele, realiza o fluxo completo de atendimento dentro das configurações do administrador, e não visualiza agenda, pacientes ou dados financeiros de outros profissionais, não altera regras de repasse nem configurações da clínica e não cadastra ou remove outros profissionais.

### 4.2 Matriz de Features por Plano

| Feature | Básico | Profissional | Enterprise |
|---|---|---|---|
| Perfil no matching + receber agendamentos | ✅ | ✅ | ✅ |
| Agenda, lembretes, prontuário básico | ✅ | ✅ | ✅ |
| Pré-sintomas do Responsável no briefing | ✅ | ✅ | ✅ |
| Receita com assinatura (MVP: nome digitado) | ✅ | ✅ | ✅ |
| NF + split (MVP: registrado, não liquidado) | ✅ | ✅ | ✅ |
| Avaliação (receber e responder) | ✅ | ✅ | ✅ |
| **IA na consulta** | — | ✅ | ✅ |
| **Documentos gerados pela IA** | — | ✅ | ✅ |
| Prontuário completo e estruturado | — | ✅ | ✅ |
| Analytics de reputação/conversão | — | ✅ | ✅ |
| Gestão multiprofissional (admin, dashboard) | — | — | ✅ |
| Analytics avançado + benchmarks | — | — | ✅ |

### 4.3 Integrações Externas

A plataforma não opera integrações com sistemas externos nesta fase.

### 4.4 Offboarding de Veterinário Vinculado

Quando o administrador desativa um veterinário da unidade, o acesso à plataforma é encerrado imediatamente. O histórico de todos os atendimentos realizados por aquele profissional permanece vinculado ao animal e à plataforma, coerente com a mente colmeia da Vetly: o prontuário pertence ao animal e estará disponível em qualquer clínica parceira onde o Responsável escolher ser atendido.

O veterinário desativado perde acesso aos prontuários, podendo solicitar apenas um extrato dos atendimentos que realizou para fins de comprovação profissional, sem acesso a dados pessoais do Responsável ou do animal, respeitando a LGPD (§7).

Os agendamentos futuros daquele veterinário são sinalizados automaticamente para o administrador, que pode redistribuir para outro profissional ou cancelar com notificação ao Responsável.

## 5. Operação Diária + Fluxo do Médico

### 5.1 Pré-Consulta — Briefing do Animal

Antes de iniciar o atendimento, o veterinário acessa o painel do animal. O sistema apresenta automaticamente um briefing com as informações relevantes para aquela consulta: histórico clínico completo do animal, medicações em uso, data e resumo da última consulta, alertas clínicos ativos e resultados de exames anteriores registrados na plataforma.

Os **pré-sintomas** informados pelo Responsável no momento do agendamento (§3.4) chegam ao veterinário neste momento, já organizados e visíveis no briefing. O veterinário entra na consulta com contexto completo, sabendo do que a consulta vai tratar, sem precisar perguntar o que já foi informado.

### 5.2 Dashboard

Ao acessar a plataforma, o veterinário visualiza o dashboard do dia: agenda do dia com todos os atendimentos programados, pacientes aguardando atendimento, alertas de animais de alto risco e retornos pendentes. Cada item é clicável e direciona para o painel do paciente correspondente. O administrador visualiza o dashboard consolidado da unidade, com a agenda de todos os veterinários e os indicadores operacionais do estabelecimento.

### 5.3 Durante a Consulta — IA de Captura

A consulta **inicia quando o veterinário aperta "iniciar consulta"** e **termina quando ele mesmo clica que acabou**; a partir daí todos os processos planejados são executados.

Durante o atendimento, a IA opera para **reduzir ao máximo a fricção do veterinário**. A IA **recebe as falas do médico durante a consulta** e captura os dados clínicos necessários para gerar diagnóstico, protocolo, prontuário, receita e demais documentos do paciente. Ao encerrar a consulta, uma automação **sobe os dados gerados para o app** e os adiciona ao histórico de vida do animal. A IA na consulta e os documentos gerados pela IA estão disponíveis nos planos Profissional e Enterprise (§4.2).

### 5.4 Fluxo Financeiro

O pagamento é processado de forma antecipada na maioria dos serviços. Consultas, vacinações, cirurgias e exames são pagos no momento do agendamento via Pix ou cartão de crédito e débito. **No MVP essa cobrança é inteiramente simulada, sem gateway de pagamento real integrado**; NF e split são **registrados, não liquidados** (§7).

Exceções ao pagamento antecipado: em internações é cobrado um valor de caução no ato da entrada e o saldo restante é apurado ao longo da internação com base nas diárias e procedimentos registrados, sendo pago na saída. Em atendimentos de emergência presencial, quando o animal chega sem agendamento prévio, o pagamento é realizado no ato do atendimento.

O split é processado conforme o plano configurado no onboarding (§9): para o autônomo, entre o profissional e a plataforma; para a empresa, entre o estabelecimento e a plataforma.

### 5.5 Fluxo de Internação

O veterinário abre uma ficha de internação vinculada ao animal. A cada dia, o profissional responsável registra os procedimentos realizados, medicações administradas e evolução clínica. O sistema apura automaticamente o valor total com base nos itens registrados e no número de diárias. O Responsável recebe atualizações diárias sobre o estado do animal no app. Ao receber alta, o sistema gera o resumo completo da internação e a nota fiscal com o saldo restante após desconto da caução, encaminhando o link de pagamento simulado. O prontuário da internação é incorporado ao histórico longitudinal do animal.

### 5.6 Fluxo de Exames

O veterinário solicita um exame dentro da plataforma durante ou após a consulta. O sistema registra a solicitação no histórico do animal e notifica o Responsável no app com as orientações necessárias para a realização do exame.

Quando o resultado estiver disponível, ele pode ser inserido de duas formas: pelo próprio veterinário via upload, ou pelo laboratório parceiro caso haja acordo estabelecido. O resultado é vinculado automaticamente ao histórico do animal e gera um alerta para o veterinário solicitante. A IA incorpora o resultado ao contexto clínico do animal para consultas futuras. O Responsável recebe o resultado no app assim que o veterinário liberar o acesso.

### 5.7 Pós-Consulta — Geração de Documentos

Ao encerrar a consulta, a partir dos dados capturados pela IA (§5.3), o sistema gera os documentos do atendimento.

Prontuário: gerado de forma completa e estruturada, registrando os dados clínicos da consulta e alimentando o histórico longitudinal do animal.

Atestado: gerado conforme a necessidade do atendimento, podendo ser de saúde, óbito ou transporte.

Receita Veterinária: preenchida pela IA com medicamento, dose, posologia e orientações. No MVP a assinatura é o **nome digitado** do profissional; em produção, assinatura digital com certificado vinculado ao CRMV. Essa etapa é obrigatória e não pode ser suprimida por exigência legal.

Nota Fiscal: gerada com split **registrado, não liquidado** no MVP, respeitando as regras de repasse do plano.

### 5.8 Correção Pós-Geração de Documentos

O veterinário pode solicitar correção de um documento já gerado dentro de um prazo de 24 horas após a consulta. O sistema não sobrescreve o documento original; gera uma versão corrigida vinculada ao registro original, com anotação automática de data, hora e CRMV do profissional. Ambas as versões ficam armazenadas no histórico do animal.

Para correções fora do prazo de 24 horas, o sistema exige uma justificativa registrada antes de liberar a edição. Esse fluxo está em conformidade com as diretrizes do CFMV para alteração de prontuários clínicos.

### 5.9 Modalidade de Atendimento

A plataforma opera com atendimentos **presenciais**. A modalidade é definida no momento do agendamento e leva em consideração a gravidade do caso e a natureza do serviço. Procedimentos que exigem presença física, como cirurgias, vacinações e exames clínicos, são presenciais. O atendimento remoto por videochamada está **fora do escopo desta fase**.

## 6. Comunicação e Notificações

### 6.1 Política de Canal

O Responsável opera dentro do app. O **Centro de Notificações in-app** concentra todos os eventos (§6.2), com preferências por categoria; **promoções exigem opt-in**. As notificações são reforçadas por **push** no dispositivo do Responsável.

### 6.2 Matriz de Canais por Evento

| Evento | In-app | Push |
|---|---|---|
| Confirmação de agendamento | ✅ | ✅ |
| Vaga aberta em lista de espera | ✅ | ✅ |
| Documentos do atendimento (receita, atestado, resumo, exames) | ✅ | ✅ |
| Confirmação de reembolso | ✅ | ✅ |
| Lembrete de vacina / vermífugo | ✅ | ✅ |
| Lembrete de retorno | ✅ | ✅ |
| Lembrete de medicação / recompra | ✅ | ✅ |
| Check-up preventivo | ✅ | ✅ |
| Orientações sazonais e dicas personalizadas | ✅ | ✅ |
| Pontos creditados / mudança de tier | ✅ | ✅ |
| Promoções | ✅ (opt-in) | ✅ (opt-in) |

### 6.3 Comunicação Proativa — Lembretes e Alertas

A plataforma monitora continuamente o histórico e o perfil do animal e dispara comunicações proativas: lembretes de vacinas e vermífugos com antecedência configurável, baseados no protocolo da espécie, raça e idade; lembretes de retorno quando o veterinário programa acompanhamento ao final da consulta; lembretes de medicação em tratamento contínuo, com alertas de dose e orientações de recompra quando o estoque estimado estiver próximo do fim; alertas de check-up preventivo por fase de vida; orientações sazonais (calor, frio, época de carrapatos e fatores ambientais da região e estação); e dicas personalizadas de nutrição, higiene e bem-estar geradas pelo perfil individual do animal, não conteúdo genérico.

### 6.4 Régua de Reenvio de Lembretes

Para cada lembrete programado, o sistema opera com uma régua de até três tentativas: primeiro envio com 7 dias de antecedência; segundo envio, sem resposta, com 3 dias; terceiro envio, sem resposta, com 1 dia. Após três tentativas sem resposta, a clínica ou veterinário responsável recebe um alerta no dashboard sinalizando o Responsável como não responsivo para aquele evento. O sistema não realiza novos envios para o mesmo evento, evitando fadiga de notificação.

## 7. LGPD e Governança

### 7.1 Coleta de Consentimento

O consentimento é coletado no onboarding do Responsável no app. O sistema apresenta de forma clara e acessível quais dados são coletados e para quais finalidades: atendimento clínico, lembretes e comunicação proativa, e compartilhamento com clínicas parceiras da rede. O consentimento é **granular**: o Responsável autoriza separadamente cada finalidade e pode revogar qualquer uma a qualquer momento no app, com registro de data e hora.

### 7.2 Colmeia por Evento Clínico

Ao agendar com um vet, se o **consentimento de rede está ativo**, o acesso ao histórico completo é concedido automaticamente àquele vet e **expira ao fim do ciclo** (consulta + 24h + retornos vinculados). Sem consentimento de rede, o vet só tem o acesso restrito clássico (o que produziu + o que o Responsável liberar). Todo acesso é registrado no **LogDeAcessoProntuário**, visível ao Responsável. Alertas de segurança (alergias/interações) nunca são ocultáveis. Dados de saúde do animal são classificados como sensíveis, com camada adicional de proteção de acesso.

### 7.3 Financeiro do Administrador

**Vê (consolidado da unidade):** faturamento bruto por período/serviço/vet; comissões/split retidos e repasses com status; NFs, reembolsos, retenções; KPIs (ticket médio, ocupação, cancelamento/no-show, receita por especialidade).

**É vedado:** dados bancários pessoais e movimentações de vets fora da unidade; remuneração interna dos vinculados — a Vetly mostra produção, nunca "salário"; dados de outros estabelecimentos.

### 7.4 Monetização de Dados

**Nível 1 — Audience Insights:** agregado, anônimo, k ≥ 100, vendável cedo. **Nível 2 — Lead Qualificado:** individual, só com opt-in destacado e opt-out fácil. Sempre respeitando os limites legais da LGPD.

### 7.5 Segurança como Argumento de Venda

A conformidade LGPD nativa é diferencial competitivo para as clínicas parceiras: ao adotar a Vetly, o estabelecimento elimina o risco jurídico de não conformidade sem precisar implementar estrutura própria de governança de dados.

## 8. Fluxos da Plataforma

**Fluxo 1 — Busca e agendamento pelo Responsável**
*Início: Responsável abre o app. Fim: agendamento confirmado e notificado.*
1. Responsável abre o app e concede geolocalização → sistema lista clínicas e vets autônomos por proximidade e serviço (RN-001)
2. Responsável filtra por necessidade (banho, tosa, emergência, rotina) e avalia por reputação/distância → lista ordenada exibida (RN-002)
3. Responsável escolhe clínica ou vet e horário → [clínica: consulta atribuída ao profissional que a clínica designar (RN-003)] / [vet autônomo: agendada diretamente com ele (RN-003)] / [sem horário: entra em lista de espera, vê outros vets ou escolhe data futura (RN-004)]
4. Responsável informa pré-sintomas do pet → dados anexados ao briefing pré-consulta (RN-005)
5. Responsável paga → agendamento confirmado; NF e split registrados, não liquidados (RN-006)
6. Sistema notifica data, horário, profissional e serviço → notificação in-app + push (RN-007) → **fim**

**Fluxo 2 — Consulta e geração de documentos**
*Início: veterinário abre o briefing. Fim: documentos no app e no histórico do animal.*
1. Veterinário abre o briefing pré-consulta → contexto completo + pré-sintomas exibidos (RN-005)
2. Veterinário clica "iniciar consulta" → sessão de captura da IA aberta (RN-008)
3. IA recebe as falas do médico durante o atendimento → dados clínicos capturados (RN-009)
4. Veterinário clica "encerrar consulta" → processos pós-consulta disparados (RN-008)
5. Sistema gera prontuário, atestado, receita e NF a partir dos dados capturados (RN-010)
6. Automação sobe os documentos para o app e os adiciona ao histórico de vida do animal (RN-011)
7. Responsável recebe os documentos → notificação in-app + push (RN-007) → **fim**

**Fluxo 3 — Cancelamento e remarcação**
*Início: Responsável abre o agendamento ativo. Fim: reembolso ou remarcação confirmados.*
1. Responsável abre o agendamento ativo no app → opções de cancelar ou remarcar exibidas (RN-012)
2. [Remarcação: novo horário selecionado → pagamento transferido para a nova data (RN-013) → **fim**]
3. [Cancelamento > 24h → reembolso integral (RN-014)]
4. [Cancelamento entre 24h e 2h → reembolso parcial, retenção configurável pela clínica no onboarding (RN-014)]
5. [Cancelamento < 2h ou no ato → sem reembolso (RN-014)]
6. Sistema confirma o reembolso quando aplicável → notificação in-app + push (RN-007) → **fim**

**Fluxo 4 — Fidelidade e resgate**
*Início: Responsável cumpre obrigação ou paga serviço. Fim: desconto resgatado no estabelecimento.*
1. Responsável cumpre obrigação do pet no prazo ou paga um serviço → pontos creditados com multiplicador do tier (RN-015)
2. Sistema recalcula acúmulo e atualiza o tier (Bronze/Prata/Ouro) → notificação in-app + push (RN-016)
3. Responsável escolhe um item listado no marketplace por uma empresa parceira → desconto em reais calculado e exibido, com a faixa de financiamento Vetly × vet aplicável (RN-017)
4. Responsável confirma a troca → pontos debitados, cupom em QR code gerado e a redução de comissão (Vetly) e de repasse (vet) registrada conforme a faixa do valor resgatado (RN-018)
5. Responsável apresenta o QR code no estabelecimento → cupom validado e desconto resgatado no item escolhido (RN-019) → **fim**

**Fluxo 5 — Avatar digital do pet**
*Início: cadastro do pet. Fim: avatar reflete o estado atual das obrigações.*
1. Cadastro do pet gera o calendário de obrigações → estado inicial do avatar definido (RN-020)
2. Obrigação vence sem cumprimento → avatar altera o estado (vacina atrasada: carinha doente; higienização atrasada: pelo maior e reclamação) (RN-021)
3. Responsável cumpre a obrigação → avatar retorna ao estado saudável (RN-021) → **fim**

**Fluxo 6 — Offboarding de vet vinculado**
*Início: administrador desativa o vet. Fim: agendamentos futuros tratados.*
1. Administrador desativa o vet → acesso à plataforma encerrado imediatamente (RN-022)
2. Histórico dos atendimentos permanece vinculado ao animal (RN-023)
3. Vet desativado perde acesso aos prontuários → pode solicitar extrato dos próprios atendimentos, sem dados pessoais (RN-024)
4. Agendamentos futuros sinalizados ao administrador → [redistribuir para outro profissional (RN-025)] / [cancelar com notificação in-app + push ao Responsável (RN-025)] → **fim**

## 9. Modelo de Monetização

Linhas de receita:
1. **Assinatura dos veterinários/clínicas** por plano (Básico, Profissional, Enterprise), com Enterprise precificado por faixa de vets (§11).
2. **Split/comissão** sobre os atendimentos processados na plataforma (registrado, não liquidado no MVP). O percentual é decrescente conforme o plano: **Básico 15%**, **Profissional 12%**, **Enterprise 10%** — a maior comissão pertence ao menor plano.
3. **Taxa de listagem no marketplace** — cobrada por categoria das empresas para que seus produtos fiquem elegíveis a resgate por pontos (§3.8). Não incide no MVP, já que o marketplace é mocado.
4. **Monetização de dados** — Audience Insights (agregado/anônimo) e Lead Qualificado (opt-in), conforme §7.4.

O **financiamento dos descontos de fidelidade** (§3.7) não é linha de receita — é redução de comissão da Vetly e do repasse do vet/estabelecimento, absorvida dentro do split de cada transação em que há resgate de pontos.

**Exemplo numérico (fechado):** clínica Enterprise com 8 vets paga assinatura mensal de **R$ 999** (faixa 6 a 10, §11). Em um atendimento de consulta de **R$ 200** com split Enterprise de **10%**, a Vetly retém **R$ 20** e o estabelecimento recebe **R$ 180**. Se o Responsável resgata **R$ 20** em pontos nesse atendimento (faixa "R$ 10,01 a R$ 30", §3.7), a Vetly absorve 60% do desconto (**R$ 12**, saindo da sua comissão de R$ 20, que passa a R$ 8) e o estabelecimento absorve 40% (**R$ 8**, saindo do seu repasse de R$ 180, que passa a R$ 172). No MVP, split e desconto são **registrados, não liquidados** — os valores são apurados e exibidos, sem repasse financeiro efetivo.

## 10. O que o MVP precisa provar

Métricas-alvo:
- Adesão do Responsável ao app: cadastro de pets, uso do board e do avatar digital.
- Conversão da busca por geolocalização em agendamento.
- Redução de fricção clínica: proporção de documentos gerados pela IA a partir da captura da consulta, sem edição manual relevante.
- Engajamento em fidelidade: acúmulo de pontos, evolução de tier e geração de cupons por QR code.
- Volume e qualidade das avaliações pós-consulta.
- Registro correto de NF e split (mesmo sem liquidação).

Fica de fora desta fase: liquidação financeira real, abatimento real de pontos no pagamento, avatar multi-espécie e com sinais dinâmicos a partir de dados reais, marketplace com transação real, atendimento remoto por videochamada e integrações externas.

## 11. Precificação por faixa

| Plano | Assinatura mensal | Take rate por atendimento |
|---|---|---|
| Básico | R$ 0 | 15% |
| Profissional | R$ 119 | 12% |
| Enterprise | por faixa de vets | 10% |

A escada troca assinatura por comissão: quanto maior o fixo, menor o percentual por transação. A diferença de 3 pontos entre Básico e Profissional paga a assinatura a partir de cerca de R$ 8.300 de faturamento mensal na plataforma.

Precificação do Enterprise por faixa de vets:

| Faixa de vets | Assinatura mensal |
|---|---|
| 1 a 5 | R$ 549 (valor-base, inclui os 5 primeiros) |
| 6 a 10 | R$ 999 |
| 11 a 20 | R$ 1.699 |
| 21+ | R$ 1.699 + R$ 70/vet acima de 20 |

---

*Documento oficial do produto Vetly — identidade, personas, telas, monetização e fluxos completos da plataforma. Este documento referencia as regras de negócio (`RN-xxx`) governadas pelo documento técnico companheiro; não as duplica nem as reescreve.*
