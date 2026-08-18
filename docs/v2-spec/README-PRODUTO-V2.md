# Vetly – Produto

> Documento oficial do produto Vetly: identidade, personas, telas, monetização e **fluxos completos da plataforma**. As regras numeradas (RN-xxx), o mapa de entidades/relacionamentos e o modelo de dados estão em **[README-TECH.md](./README-TECH.md)**, a fonte de verdade da lógica de negócio. Este documento referencia as RNs, não as duplica.

---

## 1. Identidade do Produto

**Vetly** é a plataforma intermediária entre Responsáveis de pets e veterinários – o "esquema Uber" aplicado –  saúde animal. A Vetly não presta serviço veterinário: ela conecta, organiza, cobra, reparte e guarda o histórico.

**Tese central:** o produto fideliza o cliente **com o app, não com veterinários específicos**. O compromisso da Vetly é com o Responsável. O prontuário pertence ao **animal** ("mente colmeia"), não –  clínica – logo qualquer vet da rede atende com contexto completo, e a diferenciação entre vets se desloca para **preço, qualidade, infraestrutura e equipamentos**. Esse nivelamento é um efeito estratégico desejado: estimula investimento e competição saudável, e transforma a Vetly no ativo insubstituível (os dados e a conveniência ficam na plataforma).

O Responsável opera **inteiramente dentro do app Vetly**. O WhatsApp serve exclusivamente para lembretes e promoções – nunca como canal operacional.

### Proposta de valor pelos 3 lados
| Lado | Proposta de valor |
|---|---|
| **Responsável** | Sempre há um vet próximo (busca geolocalizada), com todo o histórico do pet disponível; painel de cuidado; lembretes das obrigações do pet; descontos por fidelidade e no marketplace; pagamento e documentos em um só lugar. |
| **Veterinário / Clínica** | Demanda qualificada (pacientes chegam com briefing e pré-sintomas); IA assistente na consulta; documentos, NF e split automáticos; notoriedade via avaliação; conformidade LGPD nativa como diferencial. |
| **Plataforma (Vetly)** | Comissão por transação, assinaturas dos vets, take rate do marketplace e, no futuro, dados agregados/anonimizados – sempre dentro da LGPD. |

### 1.1 Escopo do MVP
A regra de negócio é a mesma da plataforma completa; muda o grau de implementação nesta fase.

| – rea | No MVP | Alvo (produção) |
|---|---|---|
| Busca geolocalizada | Endereço do vet persistido em banco (RN-047A); busca é **lista ordenada por distância, sem mapa**; distância do dispositivo do Responsável ao vet, com fallback por CEP/bairro (RN-047B) | Serviço de matching com geocodificação, cálculo em tempo real e **mapa visual** |
| Catálogo do marketplace | Produtos em banco de memória no front; compra leva a "em breve" (RN-090) | Catálogo próprio, Parceiro e PedidoMarketplace reais |
| Pagamentos | **Simulado, sem gateway**; split/comissão calculado e registrado, não liquidado (RN-037) | Abacate Pay (Pix/cartão) com split real |
| Assinatura da receita | **Nome digitado** (RN-031); não habilita dispensação externa de controlados | Certificado ICP-Brasil vinculado ao CRMV |
| IA na consulta | **IA real (LLM), ponta a ponta** (RN-096+) – diagnóstico, protocolo, documentos, decisão do vet Aprovar/Não aprovar/Corrigir | Igual ao MVP; refinamento de modelo/custos |
| Integrações Enterprise | Só documentadas (§4.3) | Integrações reais |
| WhatsApp | Não implementado; só push + inbox in-app (RN-104) | Canal de lembretes/promoções |
| Emergência ("SOS") | Fora do escopo | Entrada própria no app |
| Cobertura | Apenas Pinheiros/SP | Expansão por densidade |
| Multi-responsável por pet | Não – 1 Responsável por pet | Contas familiares |

---

## 2. Personas
1. **Responsável** (persona de primeira classe). Usa o app: onboarding, cadastro de pets, board do pet, busca, agendamento com pré-sintomas, carteira, avaliação, fidelidade, marketplace, notificações.
2. **Veterinário autônomo.** PJ independente; agenda própria, repasse direto.
3. **Empresa / Administrador.** Clínica/hospital/pet shop; gerencia vets vinculados, agendas, serviços e o financeiro consolidado da unidade (vedações em §4.1).
4. **Veterinário vinculado.** Escopo restrito –  própria agenda e pacientes (RN-001 a RN-006).

---

## 3. App do Responsável

### 3.1 Onboarding
1. Cadastro (nome, CPF, e-mail, telefone com verificação) **ou login social Google/Apple**.
2. **Consentimento LGPD granular** (RN-041/084): (i) atendimento clínico, (ii) lembretes/comunicação, (iii) compartilhamento na rede (mente colmeia), (iv) promoções, (v) dados anonimizados/agregados (vi) dado "Lead Qualificado" (individual, identifica a pessoa). Cada toggle é próprio e revogável no Centro de Privacidade.
3. Permissões do dispositivo: **localização** (essencial –  busca – se negada, fallback por CEP, RN-047B) e notificações push.
4. Aquisição: link de ativação da clínica, QR code no consultório, descoberta orgânica.

### 3.2 Cadastro do Pet
Nome, espécie, raça, sexo, nascimento/idade, **peso** (necessário para a IA calcular dose – RN-096.2), foto, castrado, condições pré-existentes, alergias, carteira de vacinação. Um Responsável tem N pets. Gera o **calendário de obrigações** (RN-069).

### 3.3 Board do Pet
Tela-casa de cada pet: linha do tempo de saúde; obrigações (em dia/vence/atrasado); medicações em uso; documentos consultáveis no app (RN-036); progresso de fidelidade; atalhos (agendar, relatar sintomas, marketplace).

### 3.4 Busca de Vets ("esquema Uber") – lista, sem mapa no MVP
- **Como a distância é calculada:** a lat/long do vet é derivada do endereço persistido (RN-047A); a posição do Responsável vem da permissão de localização (§3.1); a ordenação usa a distância entre os dois pontos. Se a localização for negada, ordena por bairro/CEP (RN-047B).
- **Ranking (RN-049):** no lançamento, dominado por distância + disponibilidade; a avaliação entra quando o vet atinge 3 avaliações (RN-078); vet sem avaliações aparece com selo "Novo na Vetly" e boost temporário (RN-054).
- **Filtros (RN-052):** espécie (eliminatório), especialidade, modalidade, preço,avaliação mínima, "atende hoje".
- **Card do vet:** foto, nome, CRMV, especialidades, distância, nota + nº de avaliações, faixa de preço, próximo horário, selos.
- **Perfil do vet:** serviços e valores, estrutura/equipamentos, avaliações com respostas.

### 3.5 Agendamento (no app)
1. Seleciona pet – serviço/especialidade – disponibilidade em tempo real (RN-056).
2. Modalidade presencial – remoto (procedimentos físicos são presenciais – RN-057).
3. **Anexa pré-sintomas** (texto guiado em fase de MVP) – vão ao briefing do vet e –  IA (RN-059).
4. **Checkout com pagamento simulado:** uma etapa de pagamento retorna sucesso e confirma a consulta, disparando o lock de 10 min (RN-037/RN-058). Nenhum valor real transita; a comissão/split é calculada e registrada (RN-089), nunca liquidada.
5. Sem horários: lista de espera, outros vets ou data futura (RN-038); vaga liberada notifica por push com confirmação em 1 toque (RN-060).

### 3.6 Carteira
Métodos salvos, histórico, créditos Vetly (RN-065). No MVP os valores são registros simulados.

### 3.7 Avaliação Pós-Consulta
O pop-up dispara quando o vet marca a consulta como "realizada" (RN-076). Só a nota geral é obrigatória no MVP (RN-077); 1 avaliação por consulta realizada; a nota entra no ranking a partir de 3 avaliações (RN-078).

### 3.8 Fidelidade
Cumprir obrigações no prazo gera pontos e sobe tier (Bronze/Prata/Ouro) – acúmulo e tier são reais (RN-070/071). O desconto em reais é calculado e exibido (quanto economizaria e como a incidência se divide Vetly – vet), sem abatimento real porque o pagamento é simulado (RN-072).

### 3.9 Marketplace de Parceiros
Vitrine (medicamentos, rações, comida, banho & tosa, serviços) com catálogo em memória no front. Itens aparecem com preço/desconto por tier, mas o botão de compra leva a "em breve" – sem Parceiro nem pedido real (RN-090).

### 3.10 Centro de Notificações
Inbox in-app com todos os eventos (§6.2). Preferências por categoria; promoções exigem opt-in (RN-103).

---

## 4. Onboarding do Vet / Empresa

### 4.1 Financeiro do Administrador
**Vê (consolidado da unidade):** faturamento bruto por período/serviço/vet; comissões/split retidos e repasses com status; NFs, reembolsos, retenções; KPIs (ticket médio, ocupação, cancelamento/no-show, receita por especialidade).
** – vedado:** dados bancários pessoais e movimentações de vets fora da unidade; remuneração interna dos vinculados (a Vetly mostra produção, nunca "salário" – RN-013); dados de outros estabelecimentos (RN-007).

### 4.2 Matriz de Features por Plano
| Feature | Básico | Profissional | Enterprise |
|---|---|---|---|
| Perfil no matching + receber agendamentos | – | – | – |
| Agenda, lembretes, prontuário básico | – | – | – |
| Pré-sintomas do Responsável no briefing | – | – | – |
| Receita com assinatura (MVP: nome digitado) | – | – | – |
| NF + split (MVP: registrado, não liquidado) | – | – | – |
| Avaliação (receber e responder) | – | – | – |
| **IA na consulta (diagnóstico + protocolo)** | – | – | – |
| **Documentos gerados pela IA** | – | – | – |
| Prontuário completo e estruturado | – | – | – |
| Analytics de reputação/conversão | – | – | – |
| Gestão multiprofissional (admin, dashboard) | – | – | – |
| Analytics avançado + benchmarks | – | – | – |
| Integrações externas (4.3) e API | – | – | – |
| Slots patrocinados (compra opcional) | – | – | – |

Responsáveis nunca pagam plano – a camada de usuário é gratuita. Comissão por plano: **15% / 12% / 10%** (RN-089). Assinatura: Básico R$ 0 · Profissional R$ 199/mês · Enterprise a partir de R$ 599/mês por faixa de nº de vets (§11).

### 4.3 Integrações Enterprise (documentadas)
1. Laboratórios (IDEXX, Zoetis/Vetscan); 2. NFS-e/contabilidade (Omie, Conta Azul); 3. Planos de saúde pet (Petlove, Porto.Pet, Health for Pet); 4. ERPs/PMS legados; 5. Farmácias de manipulação; 6. Calendários (Google/Outlook).

### 4.4 Offboarding de vinculado
Agendamentos futuros sinalizados para redistribuição/cancelamento; histórico preservado (RN-008/009/027); notificação ao Responsável por push + in-app (RN-028).

---

## 5. Operação Diária + Fluxo do Médico

### 5.1 Entrada do médico
Duas portas: **agenda do dia** (pacientes aguardando, alertas de alto risco, retornos – clicável ao painel) ou **link direto da consulta**. O administrador mantém o dashboard consolidado.

### 5.2 Pré-consulta – Briefing / Painel do Animal
Histórico, medicações, última consulta, alertas clínicos, exames + pré-sintomas anexados no app (RN-039).

### 5.3 Durante a consulta – IA ao lado do vet
- **Entrada da IA** (RN-096): histórico acessível ao vet naquele atendimento (colmeia – a IA não amplia acesso), pré-sintomas, espécie/raça/idade/peso.
- **Sugestão de diagnóstico** (RN-096.1): hipóteses ordenadas por probabilidade; o vet valida/descarta cada uma; nada avança sem decisão.
- **Sugestão de protocolo** (RN-096.2): medicamentos, dose calculada pelo peso, interações checadas; se faltar o peso, a IA exige o dado antes da dose.
- **Decisão do vet: Aprovar / Não aprovar / Corrigir** (RN-099): ao Corrigir, o vet reescreve por completo o diagnóstico/protocolo e esse texto vira o estado final autoritativo – a IA não re-infere nada clínico, apenas reformata para os documentos. Tudo gravado no LogDeAuditoriaIA (RN-098). Nada clínico sem aprovação ou correção explícita (RN-024/096).

### 5.4 Pós-consulta – documentos gerados pela IA
A partir do estado final, a IA gera (não nova inferência – RN-099.1): **Receita** (assinatura MVP = nome digitado, RN-031; não habilita dispensação externa de controlados, RN-091), **Prontuário** estruturado, **Atestado** (saúde/óbito/transporte), **NF + repasse** (registrado). Destinos: app do Responsável (board do pet) + banco de dados do pet; financeiro/NF – software Vetly.

### 5.5 Pós-consulta – visão do Responsável
Medicamentos da receita, higiene, orientações + preços/descontos do marketplace (botão leva a "em breve", RN-090).

### 5.6 Modalidades, internação, exames
Vídeo no app (link por push/in-app, nunca WhatsApp); internação com atualizações in-app/push e cobrança de saldo in-app; exames com resultado no app após liberação (push) – RN-036.

---

## 6. Comunicação e Notificações

### 6.1 Papel do WhatsApp
WhatsApp só lembretes e promoções; nunca documento, link de pagamento ou link de sala (RN-102). Regra de ouro: **aviso pode ir por push/WhatsApp; ação e conteúdo ficam no app.** No MVP o WhatsApp não é implementado – só push + inbox in-app.

### 6.2 Matriz de canais por evento
| Evento | In-app | Push | WhatsApp (alvo) |
|---|---|---|---|
| Confirmação de agendamento/pagamento | – conteúdo | – | – |
| Lembrete de consulta (24h/2h) | – | – | lembrete |
| Link de videochamada | – ação | – | – |
| Vaga liberada (lista de espera) | – ação | – | – |
| Documentos da consulta | – conteúdo | – | – |
| Resultado de exame | – conteúdo | – | – |
| Atualização de internação | – conteúdo | – | – |
| Cobrança de saldo de internação | – ação | – | – |
| Lembrete de obrigação do pet | – | – | lembrete |
| Lembrete de medicação/recompra | – | – | lembrete |
| Pop-up de avaliação | – ação | – | – |
| Cancelamento pelo vet | – ação | – | lembrete |
| Promoções | – | opcional | opt-in |
| Pontos/tier de fidelidade | – | opcional | – |

### 6.3 Régua de reenvio (RN-104)
3 tentativas (7/3/1 dias). No MVP: push + in-app apenas; o degrau WhatsApp é alvo. Sem resposta após a 3ª, alerta de "Responsável não responsivo" ao vet (RN-030).

---

## 7. LGPD e Governança

### 7.1 Colmeia por evento clínico (RN-083 a RN-088)
Ao agendar com um vet, se o consentimento de rede está ativo, o acesso ao histórico completo é concedido automaticamente – quele vet e expira ao fim do ciclo (consulta + 24h + retornos vinculados). Sem consentimento de rede, o vet só vê o acesso restrito clássico (o que produziu + o que o Responsável liberar). Todo acesso vai ao LogDeAcessoProntuário, visível ao Responsável. Alertas de segurança (alergias/interações) nunca são ocultáveis.

### 7.2 Monetização de dados
**Nível 1 – Audience Insights:** agregado, anônimo, k – 100, vendável cedo (RN-093). **Nível 2 – Lead Qualificado:** individual, só com opt-in destacado e opt-out fácil (RN-105).

### 7.3 Consentimento
Coleta no onboarding (RN-041/042), granular (RN-043), revogável no app com data/hora (RN-044). LGPD nativa como argumento de venda.

---

## 8. Fluxos da Plataforma

Fluxos completos ponta a ponta, em formato **passo – resultado**, com as RNs que governam cada passo.

### Fluxo 1 – Onboarding do Veterinário Autônomo
1. Vet preenche dados profissionais (CRMV, UF, especialidades, espécies, titulação) – dados registrados.
2. Sistema valida o CRMV junto ao conselho – perfil aprovado ou rejeitado; notificado se inválido/suspenso (RN-011).
3. Vet cadastra o bloco de endereço obrigatório; sistema deriva lat/long – coordenada persistida para matching (RN-047A).
4. Vet configura agenda (dias, horários, duração, intervalo) – disponibilidade publicada.
5. Vet cadastra serviços (tipo, valor,) – catálogo criado.
6. Vet cadastra dados bancários e chave Pix e configura split (registrado no MVP) – regras de repasse definidas (RN-012).
7. Vet assina plano (Básico/Profissional/Enterprise) – features liberadas (§4.2).
8. Sistema publica o perfil – vet visível no matching.

### Fluxo 2 – Onboarding da Empresa
1. Administrador cadastra a empresa – unidade registrada.
2. Administrador cadastra vets vinculados – profissionais associados; CRMV de cada um validado (RN-011).
3. Administrador cadastra o endereço da unidade; coordenada derivada – matching por unidade (RN-047A).
4. Administrador configura agenda, serviços e planos de saúde por profissional – catálogo por unidade.
5. Administrador cadastra dados bancários da empresa e configura split empresa – plataforma (registrado – RN-013) – repasse definido; remuneração interna dos vinculados fica fora da plataforma.
6. Administrador assina plano Enterprise (por faixa de nº de vets – RN-092) – features liberadas.
7. Sistema publica os perfis vinculados – vets visíveis no matching.

### Fluxo 3 – Busca e Agendamento (app do Responsável)
1. Responsável seleciona o pet e o serviço/especialidade – busca inicia.
2. Sistema calcula a distância entre o dispositivo e cada vet – **lista ordenada por distância** (sem mapa); se a localização for negada, ordena por bairro/CEP (RN-047B).
3. Sistema aplica filtros (espécie eliminatória, especialidade, modalidade, preço, plano pet, avaliação mínima, "atende hoje") e ranking (distância + disponibilidade; avaliação a partir de 3 notas; "Novo na Vetly" para entrantes) – resultado exibido (RN-048/049/054/078).
4. Responsável escolhe o vet e o horário – slot passa a `em checkout`, travado por 10 min (RN-058).
5. Responsável anexa pré-sintomas (texto guiado + mídia) – vão ao briefing do vet e –  IA (RN-059).
6. Responsável passa pelo checkout com pagamento simulado – retorno de sucesso confirma a consulta; comissão/split calculados e registrados, não liquidados (RN-037/089); slot – `confirmada`.
7. [Sem horários] Sistema oferece lista de espera, outros vets ou data futura (RN-038).
8. [Vaga liberada] 1º da fila tem prioridade por 15 min, push, confirma em 1 toque (RN-060).

### Fluxo 4 – Operação Diária + Consulta com IA
1. Vet entra pela agenda do dia ou por link direto da consulta – painel do dia (pacientes, alertas de alto risco, retornos).
2. Vet abre o paciente – briefing pré-consulta (histórico acessível naquele atendimento, medicações, última consulta, alertas, exames + pré-sintomas do app) (RN-039/096).
3. Consulta inicia (presencial ou remota; vídeo por link in-app/push, nunca WhatsApp) – sala com acesso ao painel.
4. IA apresenta hipóteses diagnósticas ordenadas por probabilidade – vet valida ou descarta cada uma; nada avança sem decisão (RN-096.1).
5. IA sugere protocolo (medicamentos, dose calculada pelo peso, interações); se faltar o peso, exige o dado antes da dose – vet revisa (RN-096.2).
6. Vet decide: **Aprovar / Não aprovar / Corrigir**. Ao Corrigir, reescreve por completo o diagnóstico/protocolo e esse texto é o estado final – a IA não re-infere, só reformata; cada decisão gravada no LogDeAuditoriaIA (RN-098/099).
7. A partir do estado final, a IA gera os documentos (não nova inferência): prontuário, receita, atestado; NF + split registrados (RN-099.1).
8. Vet "assina" a receita (nome digitado no MVP – RN-031) – documento formalizado (não habilita dispensação externa de controlados – RN-091).
9. Documentos vão ao board do pet no app + banco do pet; financeiro/NF – Vetly (registrado) (RN-036).
10. Vet marca a consulta como "realizada" – dispara o pop-up de avaliação (RN-076) e a pontuação de fidelidade (RN-075).

### Fluxo 5 – Cancelamento e No-show
1. Responsável solicita cancelamento/remarcação no app – sistema localiza o agendamento ativo.
2. [Remarcação] novo horário selecionado – registro transferido para a nova data.
3. [Cancelamento] sistema aplica as janelas (>24h / 24h – 2h / <2h) – consequência calculada e exibida, registrada, não liquidada (RN-062/063).
4. [No-show do Responsável] 3 em 90 dias – 60 dias sem descontos de fidelidade (RN-064).
5. [Cancelamento/no-show do vet] gera crédito de cortesia (10%, teto R$ 30) + strike; 3 strikes em 90 dias suspendem o perfil por 7 dias (RN-065/066/067).
6. Sistema notifica a contraparte por in-app + push (WhatsApp só lembrete de reagendamento – RN-068).

### Fluxo 6 – Fidelidade e Obrigações do Pet
1. No cadastro do pet, o sistema gera o calendário de ObrigaçõesDoPet por espécie/raça/idade – obrigações com data-limite (RN-069).
2. Sistema envia lembretes por push/in-app conforme a régua (RN-104) – Responsável avisado.
3. Obrigação cumprida no prazo via Vetly (ou consulta avulsa, peso menor) – PontosFidelidade acumulados (RN-070).
4. Ao cruzar limiares (Prata – 300 / Ouro – 800 pts em 12m), o tier é atualizado – progressão exibida (RN-071).
5. No checkout, o sistema calcula e exibe o desconto do tier e como a incidência se divide Vetly – vet – sem abatimento real (RN-072).
6. Pontos expiram em 12 meses (FIFO); cancelamento/reembolso estorna pontos (RN-074/075).

### Fluxo 7 – Colmeia por Evento Clínico (acesso ao prontuário)
1. Responsável agenda com um vet – se o consentimento de rede está ativo, o acesso ao histórico completo é concedido automaticamente – quele vet (RN-083).
2. Cada acesso gera entrada no LogDeAcessoProntuário, visível ao Responsável (RN-086).
3. O acesso expira ao fim do ciclo (consulta + 24h + retornos vinculados); depois o vet mantém só o extrato do que produziu (RN-085).
4. [Sem consentimento de rede] o vet vê apenas o acesso restrito (o que produziu + o que o Responsável liberar) (RN-085).
5. Ao trocar de vet nenhuma ação manual é necessária; o Responsável pode ocultar registros privados – exceto alertas de segurança (alergias/interações), nunca ocultáveis (RN-088).
6. [Revogação] cessa concessões futuras; registros já produzidos não são apagados (guarda CFMV), apenas bloqueados a novos acessos (RN-087).

### Fluxo 8 – Geração e Correção de Documentos
1. Após a decisão do vet na consulta, a IA gera prontuário, atestado (quando aplicável) e receita a partir do estado final – documentos preparados (RN-099.1).
2. Vet "assina" a receita (nome digitado – MVP) – etapa obrigatória concluída (RN-031).
3. Sistema gera a NF e registra o split – financeiro consolidado na Vetly (RN-089).
4. [Correção – 24h] vet solicita correção – sistema gera versão corrigida vinculada ao original (data/hora/CRMV); ambas coexistem (RN-032/033/035).
5. [Correção > 24h] vet registra justificativa – sistema libera a edição; mesma versionagem (RN-034).
6. Documentos distribuídos ao board do pet no app; Vetly consolida histórico clínico e financeiro (RN-036).

### Fluxo 9 – Internação
1. Vet abre ficha de internação vinculada ao animal – internação registrada.
2. Sistema registra caução de entrada (simulada no MVP) – valor lançado.
3. Vet registra diariamente procedimentos, medicações e evolução – valor total apurado (diárias + itens).
4. Sistema envia atualizações in-app/push ao Responsável – acompanhamento (nunca WhatsApp – RN-102).
5. Na alta, sistema gera resumo da internação e NF do saldo – cobrança de saldo in-app.
6. Prontuário da internação incorporado ao histórico do animal – histórico atualizado.

### Fluxo 10 – Exames
1. Vet solicita exame no app durante/após a consulta – solicitação registrada no histórico.
2. Sistema notifica o Responsável por push com orientações – Responsável informado.
3. [Resultado] inserido por upload do vet (ou integração de laboratório – alvo Enterprise) – vinculado ao histórico.
4. Sistema alerta o vet solicitante; a IA incorpora o resultado ao contexto clínico – disponível para consultas futuras.
5. Vet libera o acesso – Responsável recebe push e consulta o resultado no app (RN-036).

### Fluxo 11 – Triagem de Pré-sintomas (IA voltada ao Responsável)
1. Responsável relata sintomas no app (etapa distinta da IA da consulta) – triagem inicia.
2. IA coleta informações e responde com orientações gerais + disclaimer fixo (não é diagnóstico) – Responsável orientado (RN-100).
3. [Sinais de emergência] IA orienta atendimento presencial imediato e destaca vets com disponibilidade imediata – encaminhamento.
4. [Já há consulta agendada] os dados da triagem são anexados ao briefing pré-consulta – disponíveis ao vet (RN-039).

### Fluxo 12 – Notificações (régua e canais)
1. Cada evento é classificado como aviso (push/WhatsApp) ou ação/conteúdo (residente no app) – roteamento pela matriz (§6.2 – RN-101).
2. Sistema envia por push + inbox in-app (WhatsApp é alvo, não implementado – RN-102/104).
3. Para lembretes, a régua tenta 3 vezes (7/3/1 dias) – sem resposta após a 3ª, alerta de "Responsável não responsivo" ao vet (RN-030/104).
4. Promoções exigem opt-in de marketing e têm opt-out em 1 toque em toda mensagem (RN-103).

---

## 9. Modelo de Monetização

Parâmetros e regras detalhadas em [README-TECH.md](./README-TECH.md) §1 e §4-G.

1. **Comissão por transação:** 15% / 12% / 10% por plano do vet (RN-089). Principal linha.
2. **Assinaturas dos vets:** Básico R$ 0 · Profissional R$ 199/mês · Enterprise a partir de R$ 599/mês por faixa de nº de vets (§11).
3. **Marketplace:** take rate 12% (RN-090) – alvo, mockado no MVP.
4. **Dados:** Nível 1 e Nível 2 – alvo.

**Exemplo – consulta de R$ 150, vet Profissional (comissão 12% = R$ 18; repasse R$ 132), Responsável Ouro (desconto 10% = 6% Vetly + 4% vet, RN-072):** desconto R$ 15 (Vetly R$ 9, vet R$ 6) – Responsável pagaria R$ 135, vet receberia R$ 126, Vetly reteria R$ 9. No MVP isso é exibido como cálculo, não liquidado.

---

## 10. O que o MVP precisa provar
Métricas-alvo: densidade de vets ativos em Pinheiros, taxa de agendamento concluído, ticket médio (registrado), adesão – s obrigações do pet (motor da fidelidade), nota média de avaliação e uso/aprovação da IA na consulta. Recompra de marketplace e liquidação financeira não entram (mockados), para não contaminar os números.

---

## 11. Precificação do Enterprise por faixa de vets

| Faixa de vets | Assinatura mensal |
|---|---|
| 1 a 5 | R$ 599 (valor-base, inclui os 5 primeiros) |
| 6 a 10 | R$ 999 |
| 11 a 20 | R$ 1.699 |
| 21+ | R$ 1.699 + R$ 70/vet acima de 20 |

Valores-âncora, refináveis com pesquisa de mercado e previsão de custos de infra. Comissão de 10% igual em todas as faixas (RN-092).

---

*Documento oficial de produto do Vetly. Companheiro de [README-TECH.md](./README-TECH.md) (regras de negócio, entidades e relacionamentos).*
