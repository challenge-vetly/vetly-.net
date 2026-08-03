# Vetly – Regras de Negócio, Entidades e Relacionamentos

> Documento técnico do Vetly. Fonte de verdade das **regras de negócio (RN-001 a RN-107)**, do **mapa de entidades e relacionamentos** e do **modelo de dados**. Os fluxos da plataforma e a visão de produto estão em **[README.md](./README.md)**, que referencia estas RNs.
>
> **Convenções:** a persona dona do pet é **Responsável**. A operação do Responsável acontece inteiramente no **app**; o WhatsApp serve só a lembretes e promoções (não implementado no MVP). Onde uma regra é **alvo de produção** e não entra no MVP, isso está sinalizado na própria regra – sem removê-la.

---

## 1. Parâmetros de Lançamento (Pinheiros/SP)

Fonte única dos números citados pelas RNs. Ajustáveis com dados reais.

| Domínio | Parâmetro | Default de lançamento | RN |
|---|---|---|---|
| Matching | Raio de busca (lançamento) | **5 km em Pinheiros, expansível a 15 km** (padrão genérico de produto: 10 km – RN-047) | RN-047 |
| Matching | Pesos do score | distância 35% · avaliação 30% · disponibilidade 20% · confiabilidade 15% | RN-049 |
| Matching | Slot patrocinado | 1 por página, nota – 4,0, cobrança por período | RN-053/095 |
| Agendamento | Lock de checkout | 10 minutos | RN-058 |
| Lista de espera | Prioridade do 1º da fila | 15 minutos | RN-060 |
| No-show | Limiar de penalidade | 3 no-shows em 90 dias – 60 dias sem descontos | RN-064 |
| Cancelamento pelo vet | Crédito de cortesia | 10% do valor, teto R$ 30 | RN-065 |
| Fidelidade | Tiers | Prata – 300 pts/12m · Ouro – 800 pts/12m | RN-071 |
| Fidelidade | Desconto serviços | Prata 5% (3% Vetly + 2% vet) · Ouro 10% (6% Vetly + 4% vet) | RN-072 |
| Fidelidade | Desconto marketplace | Prata 10% · Ouro 15% | RN-071 |
| Fidelidade | Expiração de pontos | 12 meses (FIFO) | RN-074 |
| Avaliação | Nota pública a partir de | 3 avaliações; recência 90 dias pesa 2 – | RN-078 |
| Avaliação | Janela de edição | 48 horas | RN-082 |
| Planos | Assinatura fixa | Básico R$ 0 · Profissional R$ 199/mês | RN-092 |
| Planos | Enterprise por faixa de vets | R$ 599 (1 – 5) · R$ 999 (6 – 10) · R$ 1.699 (11 – 20) · +R$ 70/vet acima de 20 | RN-092 |
| Comissão | Take rate de serviço | **15% Básico / 12% Profissional / 10% Enterprise** | RN-089 |
| Marketplace | Take rate | 12% | RN-090 |
| Dados | k-anonimato Nível 1 | k – 100 por célula | RN-093 |
| Notificações | Régua de reenvio | 3 tentativas (push, push, WhatsApp-lembrete) – WhatsApp é alvo, não MVP | RN-104 |

---

## 2. Mapa de Entidades e Relacionamentos

Cada entidade lista atributos principais e relacionamentos com cardinalidade. Os atributos completos e o status MVP das entidades novas estão na §5 (Modelo de Dados); esta seção é o mapa relacional.

### ENTIDADE: Responsável
Atributos: dados pessoais confirmados no onboarding do app (ou login social Google/Apple), consentimento LGPD granular por 5 finalidades (data/hora de registro e de revogação), dispositivos/push tokens, geolocalização autorizada (fallback CEP/bairro quando negada – RN-047B), preferências de notificação por categoria, tier de fidelidade, saldo de pontos, saldo de créditos Vetly, contador de no-shows (janela móvel de 90 dias).
Relacionamentos:
- Possui **Animal** (1:N) – um Responsável pode ter múltiplos pets; 1 Responsável por pet
- Agenda **Consulta** (1:N) – no app, com pré-sintomas anexados (RN-056/RN-059)
- Realiza **Pagamento** (1:N) – no MVP, registro simulado, sem liquidação (RN-037)
- Recebe **Documento** (1:N) – receita, atestado, prontuário e resultados no board do pet (RN-036)
- Escreve **Avaliação** (1:N) – uma por consulta realizada (RN-076)
- Acumula **PontosFidelidade** (1:N) – por obrigações cumpridas e consultas (RN-070)
- Recebe **Notificação** (1:N) – push + inbox in-app (RN-101)
- Faz **PedidoMarketplace** (1:N) – alvo, não MVP (marketplace mockado – RN-090)

### ENTIDADE: Veterinário
Atributos: CRMV, UF de atuação, especialidades, espécies atendidas, titulação acadêmica, agenda (dias/horários, duração média, intervalo), serviços oferecidos (tipo, valor), dados bancários (banco, agência, conta, CPF/CNPJ do titular), chave Pix, plano de assinatura (Básico/Profissional/Enterprise), persona (autônomo ou vinculado). **Campos de endereço embutidos** (CEP, logradouro, número, complemento, bairro, cidade, UF, ponto de referência, **latitude/longitude derivadas do endereço** – RN-047A; modelo 1:1, sem tabela separada). Métricas de matching: nota média ponderada por recência, nº de avaliações, taxa de conclusão (confiabilidade), strikes ativos, status no matching (ativo/suspenso), flag de patrocínio ativo, tempo médio de resposta.
Relacionamentos:
- Realiza **Consulta** (1:N)
- Solicita **Exame** (1:N)
- Acompanha **Internação** (1:N)
- Assina **Documento** (1:N) – receita; no MVP a assinatura é o nome digitado (RN-031)
- Recebe **Avaliação** (1:N) e Responde **Avaliação** (1:1 por avaliação – RN-079)
- Pertence a **Empresa** (N:1) – quando vinculado
- Recebe **Pagamento** (1:N) – repasse persona autônoma; no MVP registrado, não liquidado
- Gera **LogDeAuditoriaIA** (1:N) e gera **LogDeAcessoProntuário** (1:N)

### ENTIDADE: Empresa
Atributos: tipo (clínica, hospital veterinário, pet shop com serviços clínicos, estabelecimento com múltiplos profissionais), planos de saúde aceitos, serviços por profissional, faixa de assinatura Enterprise por nº de vets (RN-092).
Relacionamentos:
- Tem **Administrador** (1:1)
- Emprega **Veterinário** vinculado (1:N)
- Origina **Pagamento** (1:N) – repasse da unidade; no MVP registrado, não liquidado

### ENTIDADE: Administrador
Atributos: escopo de acesso **consolidado da unidade, com vedações financeiras** (RN-007) – sem dados bancários pessoais de vets, sem remuneração interna dos vinculados, sem dados de outros estabelecimentos (README.md §4.1).
Relacionamentos:
- Gerencia **Empresa** (1:1)
- Gerencia **Veterinário** vinculado (1:N) – cadastra, edita, desativa
- Redistribui **Consulta** (1:N) – agendamentos de vet desativado sinalizados para redistribuição/cancelamento (RN-028)

### ENTIDADE: Animal
Atributos: espécie, raça, sexo, nascimento/idade, **peso** (obrigatório para a IA sugerir dose – RN-096.2), foto, castrado, condições pré-existentes, alergias, carteira de vacinação, histórico clínico longitudinal, medicações em uso, alertas clínicos ativos, flags de registros ocultados pelo Responsável (exceto alertas de segurança – RN-088).
Relacionamentos:
- Pertence a **Responsável** (N:1)
- Tem **Prontuário** (1:N) – vinculado ao animal, não ao vet (RN-083)
- Tem **Consulta** (1:N)
- Tem **Internação** (1:N)
- Tem **Exame** (1:N)
- Tem **ObrigaçãoDoPet** (1:N) – calendário derivado de espécie/raça/idade (RN-069)
- Tem **LogDeAcessoProntuário** (1:N) – todo acesso registrado e visível ao Responsável (RN-086)

### ENTIDADE: Consulta
Atributos: data, horário, modalidade (presencial/remota – RN-057), vet responsável, pré-sintomas anexados (estruturado + mídia – RN-059), **estado do slot/consulta** (livre / em checkout / confirmada / realizada / cancelada / no-show – RN-058/RN-061), lock de checkout (10 min – RN-058), contador de remarcações, status de no-show (Responsável/vet), diagnóstico aprovado, protocolo aprovado, referência –  **Avaliação**
Relacionamentos:
- Realizada por **Veterinário** (N:1)
- Sobre **Animal** (N:1)
- Agendada por **Responsável** (N:1) – no app (RN-056)
- Gera **Documento** (1:N) – do estado final (RN-099.1)
- Tem **Pagamento** (1:1) – simulado, confirma a consulta + dispara o lock (RN-037/RN-058)
- Gera **Avaliação** (1:0..1) – disparada quando o vet marca "realizada" (RN-076)
- Gera **PontosFidelidade** (1:N) – só se confirmada e realizada (RN-075)
- Gera **LogDeAuditoriaIA** (1:N) – cada decisão da IA/vet (RN-098)

### ENTIDADE: Prontuário
Atributos: dados clínicos completos, histórico longitudinal do animal, versão original, versão corrigida (data, hora, CRMV do solicitante – RN-032/RN-035).
Relacionamentos:
- Pertence a **Animal** (N:1) – do animal, não do vet/clínica (RN-026/RN-083)
- Gerado por **Consulta** (N:1)
- Gerado por **Internação** (N:1)
- Referencia **Prontuário** (1:0..1) – auto-relacionamento original – versão corrigida; ambas coexistem

### ENTIDADE: Exame
Atributos: tipo de solicitação, resultado (upload pelo vet ou via integração com laboratório parceiro – integração é alvo Enterprise, README.md §4.3), status de liberação ao Responsável.
Relacionamentos:
- Solicitado por **Veterinário** (N:1)
- Vinculado a **Animal** (N:1) – resultado incorporado ao histórico; Responsável recebe push, conteúdo no app (RN-036)

### ENTIDADE: Internação
Atributos: procedimentos diários, medicações administradas, evolução clínica diária, valor total apurado (diárias + procedimentos), caução de entrada, saldo restante na saída. No MVP os valores são registrados, não liquidados.
Relacionamentos:
- Vinculada a **Animal** (N:1)
- Acompanhada por **Veterinário** (N:1)
- Gera **Documento** (1:N) – resumo da internação e NF do saldo
- Tem **Pagamento** (1:N) – caução + saldo; atualizações e cobrança in-app/push, nunca WhatsApp (RN-102)

### ENTIDADE: Documento
Atributos: tipo (Prontuário, Atestado saúde/óbito/transporte, Receita Veterinária, Nota Fiscal), versão (original/corrigida), data/hora de geração, CRMV signatário (receita), **assinatura – MVP: nome digitado; produção: certificado ICP-Brasil (RN-031)**.
Relacionamentos:
- Gerado por **Consulta** (N:1) – do estado final aprovado/corrigido pelo vet (RN-099.1)
- Gerado por **Internação** (N:1)
- Assinado por **Veterinário** (N:1) – obrigatório para receita
- Recebido por **Responsável** (N:N) – destino: board do pet no app (nunca WhatsApp – RN-102)

### ENTIDADE: Pagamento
Atributos: **no MVP é registro simulado** (status sucesso/config., valor, comissão calculada por RN-089, incidência do desconto de fidelidade RN-072); tipos: consulta, pedido de marketplace (alvo), crédito Vetly (emissão/consumo); meio-alvo de produção (Pix/cartão via Abacate Pay); momento (antecipado no agendamento / caução + saldo na internação). Nenhum valor liquidado no MVP.
Relacionamentos:
- Realizado por **Responsável** (N:1) – pagador em todos os cenários
- Associado a **Consulta** (N:1)
- Associado a **Internação** (N:1)
- Vinculado a **Veterinário** (N:1) – repasse persona autônoma (registrado)
- Vinculado a **Empresa** (N:1) – repasse persona empresa (registrado)

### ENTIDADE: Avaliação
Atributos: nota geral (1 – 5, obrigatória – única obrigatória no MVP – RN-077), subnotas opcionais (atendimento, pontualidade, estrutura, custo-benefício), comentário, data, status de moderação, resposta do vet (0..1), peso de recência.
Relacionamentos:
- Sobre **Consulta** (N:1) – única por consulta realizada (RN-076)
- Escrita por **Responsável** (N:1)
- Recebida por **Veterinário** (N:1) – nota entra no ranking a partir de 3 avaliações (RN-078)

### ENTIDADE: ObrigaçãoDoPet
Atributos: tipo (vacina/vermífugo/retorno/check-up), data-limite, status (em dia/vence/atrasado).
Relacionamentos:
- Pertence a **Animal** (N:1) – gerada no cadastro do pet (RN-069)
- Cumprida por **Consulta** (0..1) – serviço realizado até a data-limite
- Gera **Notificação** (1:N) e gera **PontosFidelidade** (1:N)

### ENTIDADE: PontosFidelidade
Atributos: evento de origem, pontos, data, expiração (12m, FIFO – RN-074), estorno. No MVP acúmulo e tier são reais; o desconto em reais é calculado/exibido, sem abatimento (RN-072).
Relacionamentos:
- Acumulados por **Responsável** (N:1)
- Originados de **Consulta** ou **ObrigaçãoDoPet** (N:1)

### ENTIDADE: Notificação
Atributos: evento, classificação (aviso – ação/conteúdo – RN-101), canal(is), tentativa da régua (RN-104), status de leitura.
Relacionamentos:
- Recebida por **Responsável** ou **Veterinário** (N:1)
- Originada de um evento (N:1). No MVP: push + inbox in-app; WhatsApp é alvo.

### ENTIDADE: LogDeAuditoriaIA
Atributos: timestamp, versão do modelo, tipo de sugestão, conteúdo sugerido, decisão (aprova/rejeita/corrige), **conteúdo final**, CRMV. Log **imutável**, retido junto ao Prontuário (RN-098).
Relacionamentos:
- Vinculado a **Consulta** (N:1)
- Vinculado a **Veterinário** (N:1)

### ENTIDADE: LogDeAcessoProntuário
Atributos: quem, quando, contexto (consulta X), base de acesso (consentimento de rede – atendimento direto). **Visível ao Responsável** no Centro de Privacidade (RN-086).
Relacionamentos:
- Vinculado a **Animal** (N:1)
- Vinculado a **Veterinário** (N:1)

### ENTIDADE: Parceiro *(alvo, não MVP)*
Atributos: razão social, categorias, take rate contratado (12% – RN-090), SLA de entrega. Catálogo mockado no MVP.
Relacionamentos:
- Oferece **Produto** (1:N)
- Recebe **PedidoMarketplace** (1:N)

### ENTIDADE: PedidoMarketplace *(alvo, não MVP)*
Atributos: itens, valor, desconto (e incidência), status, rastreio, ref. –  Receita. Compra leva a "em breve" no MVP (RN-090).
Relacionamentos:
- Feito por **Responsável** (N:1)
- Atendido por **Parceiro** (N:1)
- Tem **Pagamento** (1:1)
- Referencia **Receita/Documento** (0..1)

---

## 3. Regras de Negócio – Base (RN-001 a RN-046)

Regras fundamentais de operação, vinculação, financeiro, documentos, IA de triagem e LGPD. A coluna **Escopo** indica o grau de implementação no MVP.

| RN | Regra | Escopo |
|---|---|---|
| RN-001 a RN-006 | Veterinário vinculado opera restrito –  própria agenda e aos próprios pacientes; não vê agenda, financeiro ou pacientes de outros vets da unidade. | MVP |
| RN-007 | O Administrador tem acesso **consolidado da unidade com vedações financeiras**: vê produção (faturamento bruto, comissões/repasses, NFs, KPIs), não vê dados bancários pessoais de vets, remuneração interna dos vinculados nem dados de outros estabelecimentos. | MVP |
| RN-008, RN-009 | Offboarding de vet vinculado: agendamentos futuros são sinalizados para redistribuição ou cancelamento; histórico produzido é preservado. | MVP |
| RN-010 | Acesso ao prontuário segue o modelo de **colmeia por evento clínico** (RN-083+): com consentimento de rede ativo, o vet com consulta agendada vê o histórico completo, com expiração ao fim do ciclo; sem consentimento de rede, vale o acesso restrito (o que o vet produziu + o que o Responsável liberar). | MVP |
| RN-011 | CRMV é validado junto ao conselho no onboarding; perfil com registro inválido ou suspenso não é publicado no matching. | MVP |
| RN-012 a RN-018 | Split e repasse por transação conforme plano do vet; NF automática; repasse via Abacate Pay em produção. **No MVP o pagamento é simulado – split calculado e registrado, não liquidado** (RN-037). | Split: MVP (registrado) · Liquidação: produção |
| RN-019 a RN-023 | Política de reembolso por janela de cancelamento; serve de base – s regras de no-show (RN-062+). No MVP o reembolso é registrado, não liquidado. | MVP (registrado) |
| RN-024 | Nenhum conteúdo clínico (diagnóstico, protocolo, documento) é emitido sem aprovação ou correção explícita do vet. | MVP |
| RN-025 | Procedimentos físicos são sempre presenciais; telemedicina é limitada a orientação e triagem. | MVP |
| RN-026, RN-027 | O prontuário pertence ao animal e acompanha-o entre vets/unidades; nenhuma clínica "retém" o histórico. | MVP |
| RN-028 | Redistribuição/cancelamento por offboarding notifica o Responsável por in-app + push (WhatsApp só lembrete de reagendamento, alvo). | MVP (push+in-app) |
| RN-029 | Régua de reengajamento de 3 tentativas (7/3/1 dias) com escalonamento de canal. **No MVP: push + in-app; degrau WhatsApp é alvo.** | MVP (parcial) |
| RN-030 | Após a 3ª tentativa sem resposta, o vet/clínica recebe alerta de "Responsável não responsivo". | MVP |
| RN-031 | Receita exige assinatura do vet. **No MVP a assinatura é o nome digitado**; não habilita dispensação externa de controlados. Produção: certificado ICP-Brasil vinculado ao CRMV. | MVP (nome digitado) · ICP: produção |
| RN-032 a RN-035 | Correção de documentos gera versão corrigida vinculada ao original (data, hora, CRMV); original e correção coexistem; correção após 24h exige justificativa. | MVP |
| RN-036 | Documentos e resultados de exame ficam no **app** (board do pet); o Responsável recebe push, nunca o conteúdo por WhatsApp. | MVP |
| RN-037 | Consulta só é confirmada após pagamento. **No MVP o pagamento é uma etapa simulada** que retorna sucesso e dispara confirmação + lock de 10 min (RN-058); nenhum valor real transita; comissão/split calculado e gravado, não liquidado. | MVP (simulado) |
| RN-038 | Sem horário disponível, o app oferece lista de espera, outros vets ou data futura. | MVP |
| RN-039 | Pré-sintomas anexados pelo Responsável no app (no agendamento) alimentam o briefing pré-consulta e a IA da consulta (RN-096). | MVP |
| RN-040 | IA de triagem opera in-app, vedada a orientação clínica complexa, sempre encaminha a atendimento; em emergência orienta presencial imediato. Etapa distinta da IA da consulta (RN-100). | MVP |
| RN-041 | No primeiro acesso do app, confirmação de dados, cadastro do pet e consentimento LGPD antes de qualquer outra ação. | MVP |
| RN-042, RN-043 | Consentimento coletado de forma granular no primeiro contato, por finalidade. | MVP |
| RN-044 | Revogação de consentimento no Centro de Privacidade do app, com registro de data/hora. | MVP |
| RN-045 | Dados de saúde do animal são sensíveis e recebem camada adicional de proteção. | MVP |
| RN-046 | Uso de dados agregados tem salvaguardas: k-anonimato mínimo, granularidade máxima definida e opt-out específico (RN-093/094/105). | Salvaguardas: MVP · Venda de dados: produção |

---

## 4. Regras de Negócio – Plataforma (RN-047 a RN-107)

Formato: **enunciado | justificativa**. O status de MVP, quando relevante, está no próprio enunciado.

### A. Matching e Geolocalização
- **RN-047** | A busca lista vets/clínicas num raio padrão de produto de 10 km, expansível pelo Responsável. No lançamento em Pinheiros o raio default é 5 km, expansível a 15 km (§1). | Densidade urbana típica; evita lista vazia sem poluir com opções inviáveis.
- **RN-047A** | Todo vet/unidade tem **endereço persistido em banco** nos campos de endereço do próprio registro (modelo 1:1, sem tabela separada), cadastrado num bloco obrigatório do CRUD do vet; a latitude/longitude é **derivada desse endereço** e é a fonte usada na busca e no cálculo de proximidade – nunca dado mockado no front. | Localização é dado de negócio auditável e reaproveitável; não pode viver só no cliente.
- **RN-047B** | A distância é calculada entre a **posição do Responsável** (permissão de localização do dispositivo) e a **coordenada do vet** (RN-047A). Se a localização for negada, o fallback ordena por bairro/CEP informado pelo Responsável; sem CEP, lista por disponibilidade dentro de Pinheiros. | Sem fallback o fluxo de busca trava quando a permissão é negada.
- **RN-048** | Espécie atendida compatível com o pet é **filtro eliminatório**; vet que não atende a espécie nunca aparece. | Evita matching clinicamente inválido.
- **RN-049** | A ordenação combina 4 fatores numa nota única: **(1) distância, (2) avaliação** (recência pesa mais – RN-078), **(3) disponibilidade** ("atende hoje"/próximas 48h), **(4) confiabilidade** (histórico de não cancelar). Pesos default 35/30/20/15 (§1). No lançamento, com base de avaliações rala, o score é dominado por distância + disponibilidade; a avaliação entra ao atingir o mínimo (RN-078), e vet sem avaliações é tratado pela RN-054. | Traduz o "esquema Uber" e garante que o ranking não nasça quebrado.
- **RN-050** | Preço não entra no score padrão, mas é filtro e critério de ordenação opcional ("menor preço"). | Preserva neutralidade e serve o Responsável sensível a preço.
- **RN-051** | Desempate: maior nota – menor distância – menor tempo médio de resposta a agendamentos. | Critério determinístico e auditável.
- **RN-052** | Filtros: especialidade, modalidade, preço,avaliação mínima, "atende hoje". | Origem: definição de produto (agendar por especialidade).
- **RN-053** | No máximo **1 slot patrocinado por página**, rotulado "Patrocinado", elegível só a vets que passam todos os filtros e têm nota – 4,0. | Monetiza visibilidade sem quebrar a confiança no ranking.
- **RN-054** | Vet sem avaliações recebe selo "Novo na Vetly" e boost temporário de 30 dias equivalente –  nota mediana da região. | Resolve cold start sem punir entrantes.
- **RN-055** | Vet sem nenhum horário aberto nos próximos 7 dias cai para o fim da lista (não some). | Disponibilidade é o núcleo da promessa "sempre há um vet perto de você".

### B. Agendamento (máquina de estados do slot)
- **RN-056** | O agendamento parte de pet + serviço/especialidade e mostra disponibilidade em tempo real por vet. | Definição de produto.
- **RN-057** | Presencial – remoto definido no agendamento; procedimentos físicos são sempre presenciais (reafirma RN-025). | Segurança clínica.
- **RN-058** | **Máquina de estados do slot:** `livre – em checkout (locked 10 min) – confirmado`, ou ` – liberado` se o lock expira. O checkout inicia ao entrar no pagamento simulado; a confirmação (fake, retorna sucesso) transiciona para `confirmado`. | Impede overbooking sem gateway real; torna o lock testável.
- **RN-059** | Pré-sintomas (texto guiado + fotos/vídeos) anexados no agendamento vão ao briefing do vet e –  IA da consulta (RN-096). | Qualifica a demanda.
- **RN-060** | Lista de espera: liberado um slot, o 1º da fila tem **prioridade por 15 min** (push, confirmação em 1 toque); expirado, passa ao próximo. | Ordena a fila sem trava manual.
- **RN-061** | Estados da consulta: `confirmada – realizada` (o vet marca) `| cancelada | no-show`. "Realizada" dispara a avaliação (RN-076) e a pontuação (RN-075). | Fonte única do ciclo de vida da consulta.

### C. Cancelamento e No-show
- **RN-062** | Reaproveita a política de reembolso (RN-019 a RN-021) como base; no MVP o reembolso é registrado, não liquidado. | Continuidade sem fingir liquidação.
- **RN-063** | Cancelamento pelo Responsável segue as janelas (>24h, 24h – 2h, <2h); no MVP a consequência é calculada e exibida. | Coerência com pagamento simulado.
- **RN-064** | No-show do Responsável: 3 em 90 dias – 60 dias sem descontos de fidelidade. | Antifraude do programa.
- **RN-065** | Cancelamento pelo vet gera crédito de cortesia (10% do valor, teto R$ 30, como crédito Vetly). | Compensa a quebra de expectativa.
- **RN-066** | No-show do vet (Responsável presente, vet ausente 15 min): tratado como cancelamento pelo vet (RN-065) + strike de reputação. | Simetria de responsabilidade.
- **RN-067** | A taxa de cancelamento/no-show do vet alimenta o componente "confiabilidade" (RN-049); 3 strikes em 90 dias suspendem o perfil do matching por 7 dias, com aviso prévio. | Reputação operacional vira consequência econômica.
- **RN-068** | Todo cancelamento notifica a contraparte por in-app + push; WhatsApp só como lembrete de reagendamento (alvo). | Coerência com a matriz de canais.

### D. Programa de Fidelidade e Descontos
- **RN-069** | "Obrigações do pet" = eventos do protocolo por espécie/raça/idade (vacinas, vermífugos, retornos, check-ups), gerados no cadastro do pet e atualizados a cada consulta. | Define o objeto do programa.
- **RN-070** | Obrigação cumprida **no prazo** (via Vetly) gera pontos; consulta avulsa paga também pontua (peso menor). No MVP o acúmulo de pontos e a exibição de tier/progresso funcionam de verdade. | Recompensa o comportamento que gera saúde e recorrência.
- **RN-071** | Tiers: Bronze (padrão), Prata ( – 300 pts/12m), Ouro ( – 800 pts/12m); descontos de 0%/5%/10% em serviços e 5%/10%/15% no marketplace. | Progressão simples e comunicável.
- **RN-072** | **Incidência do desconto de serviço: compartilhada** (Prata 5% = 3% Vetly + 2% vet; Ouro 10% = 6% Vetly + 4% vet), informada ao vet na adesão. No MVP o desconto é **calculado e exibido** (quanto o Responsável economizaria e a divisão da incidência), **sem abatimento financeiro real** (pagamento simulado). | Mecânica testável sem fingir liquidação; os dois lados dividem o custo da fidelização.
- **RN-073** | **Incidência do desconto no marketplace: sobre a margem do parceiro**, com co-funding opcional da Vetly. Não exercitado no MVP (marketplace mockado – RN-090). | Padrão de varejo; mantém take rate previsível.
- **RN-074** | Pontos expiram em 12 meses (FIFO); tier reavaliado a cada ciclo de 12 meses. | Estimula recorrência sem punição abrupta.
- **RN-075** | Só pontuam eventos com consulta/serviço confirmado e realizado; cancelamentos e reembolsos estornam pontos. | Antifraude básica.

### E. Avaliação e Notoriedade
- **RN-076** | **Gatilho:** o pop-up de avaliação dispara quando o vet marca a consulta como "realizada". O Responsável tem 7 dias; relação **1 avaliação : 1 consulta realizada**. | Definição de produto + antifraude estrutural.
- **RN-077** | Dimensões: **nota geral (1 – 5, obrigatória – única obrigatória no MVP)** e subnotas opcionais (atendimento, pontualidade, estrutura, custo-benefício); comentário opcional. | Nota simples para o ranking, subnotas para diagnóstico do vet.
- **RN-078** | A nota que entra no matching é a média ponderada por recência (últimos 90 dias pesam 2 – ); **só é exibida publicamente e só entra no ranking a partir de 3 avaliações** – antes vale a RN-054. | Reflete qualidade atual e evita nota volátil.
- **RN-079** | O vet tem **1 resposta pública** por avaliação, sujeita –  moderação. | Direito de defesa sem flame war.
- **RN-080** | Moderação: comentários com dados pessoais, ofensas ou fora de escopo são ocultados; a **nota permanece** salvo fraude comprovada. | Modera o texto sem permitir gestão de nota via denúncia.
- **RN-081** | Antifraude: avaliações de consultas canceladas/reembolsadas são invalidadas; padrões anômalos vão a revisão; fraude comprovada gera strike. | Protege o ativo central de notoriedade.
- **RN-082** | Notas não editáveis após 48h da publicação. | Estabilidade do ranking com janela curta de arrependimento.

### F. Dados do Pet ("mente colmeia") – LGPD
- **RN-083** | O prontuário pertence ao animal (reafirma RN-026); a portabilidade é mediada por **evento clínico**: ao agendar com um vet, **se o consentimento de rede está ativo, o acesso ao histórico completo é concedido automaticamente** – quele vet. | Reconciliação formal da tese com RN-010/RN-045.
- **RN-084** | O consentimento de "compartilhamento na rede" é finalidade granular própria, opt-in explícito no onboarding. | LGPD: finalidade específica, informada, destacável.
- **RN-085** | O acesso concedido por agendamento **expira ao fim do ciclo (consulta + 24h + retornos vinculados)**; depois o vet mantém só o extrato do que produziu. Sem consentimento de rede, vale o acesso restrito (RN-010). | Minimização de acesso.
- **RN-086** | Todo acesso gera entrada no **LogDeAcessoProntuário** (quem, quando, contexto, base de acesso), **visível ao Responsável**. | Transparência é o que torna a colmeia aceitável juridicamente.
- **RN-087** | Revogação do consentimento de rede cessa concessões futuras, mas não apaga registros já produzidos (guarda CFMV); ficam bloqueados a novos acessos, não destruídos. | Concilia direito do titular com obrigação regulatória.
- **RN-088** | Ao trocar de vet, nenhuma ação manual é necessária; o Responsável pode ocultar registros privados – **exceto alertas de segurança (alergias e interações), que nunca são ocultáveis**. | Portabilidade sem fricção, com trava de segurança do animal.

### G. Monetização
- **RN-089** | Receita de serviços segue o split (RN-012 a RN-018): **comissão de 15% (Básico) / 12% (Profissional) / 10% (Enterprise)** por transação, NF automática e repasse via Abacate Pay (produção). No MVP o valor é calculado e registrado, não liquidado. | Core financeiro; fonte de verdade do take rate.
- **RN-090** | Marketplace: quem vende e entrega é o **parceiro** (seller of record); a Vetly intermedeia o checkout e retém take rate de **12%**. No MVP é mockado (catálogo em memória no front): itens têm preço/desconto exibidos, mas a compra leva a "em breve" – sem Parceiro nem PedidoMarketplace reais. | Evita estoque/logística próprios; mantém as métricas limpas.
- **RN-091** | Itens de receita médica no marketplace só são vendidos por parceiros habilitados e **contra receita assinada digitalmente**. Enquanto a assinatura for o nome digitado (RN-031), a receita não é válida para dispensação externa de controlados. | Compliance sanitário.
- **RN-092** | Assinaturas dos vets (README.md §4.2): Básico e Profissional com preço fixo; **Enterprise por faixa de nº de vets** (valor-base cobre os 5 primeiros; sobe por degraus – §1). Troca de faixa automática ao cruzar o limite. | Segunda linha de receita; preço acompanha o tamanho da rede.
- **RN-093** | **Nível 1 – Audience Insights:** venda só de dados agregados/anonimizados, k – 100 por célula, dimensões máximas região – espécie – faixa etária – categoria clínica, contrato vedando reidentificação. Não é dado pessoal; não exige consentimento individual. Não exercitado no MVP. | Salvaguarda técnica da monetização de dados.
- **RN-094** | O Responsável tem opt-out específico da finalidade "dados agregados", sem perda de funcionalidade. | LGPD: consentimento não é condição de uso quando não é essencial.
- **RN-095** | Slots patrocinados (RN-053) cobrados por período, disponíveis a planos Profissional+. | Terceira linha de receita, limitada para não corroer confiança.

### H. IA na Consulta – Responsabilidade e Limites
- **RN-096** | **Entrada da IA por etapa:** a IA recebe (a) histórico acessível ao vet **naquele atendimento** (colmeia, RN-083/085 – a IA não amplia o acesso do vet), (b) pré-sintomas (RN-039/RN-059) e (c) espécie/raça/idade/peso. Nenhum documento clínico é emitido sem decisão explícita do vet (reafirma RN-024). | A IA assiste; o vet responde; o acesso da IA é o mesmo do vet, nunca maior.
- **RN-096.1 – Sugestão de diagnóstico** | A IA retorna **hipóteses ordenadas por probabilidade**; o vet valida ou descarta cada uma. Nada avança para o protocolo sem ao menos uma decisão do vet. | Estado clínico explícito; nada roda "no automático".
- **RN-096.2 – Sugestão de protocolo** | A IA sugere medicamentos, **doses calculadas pelo peso**, posologia e **alertas de interação** checados contra as medicações em uso. **Se o peso não estiver cadastrado, a IA exige o peso antes de sugerir qualquer dose.** | – onde mora o erro clínico: dose sem peso não pode ser sugerida.
- **RN-097** | A IA pode gerar sozinha apenas artefatos **não clínicos**: rascunhos, resumos de briefing, lembretes e orientações gerais pré-aprovadas. | Delimita o perímetro autônomo da IA.
- **RN-098** | **Trilha de auditoria (LogDeAuditoriaIA):** cada decisão grava sugestão, decisão (aprova/rejeita/corrige), **conteúdo sugerido vs. conteúdo final**, versão do modelo, CRMV e timestamp; a correção do vet e a versão final divergente ficam ambas registradas. Log imutável, retido junto ao prontuário. | Defesa jurídica de plataforma e vet + insumo de melhoria do modelo.
- **RN-099 – Decisão do vet: Aprovar / Não aprovar / Corrigir** | A IA apresenta diagnóstico/protocolo com três opções. **Aprovar:** o estado da IA vira o estado final. **Corrigir:** o vet edita **por completo** a hipótese e/ou o protocolo – o texto que ele deixa é o **estado final autoritativo**; **a IA não re-infere nada clínico** (não recalcula diagnóstico, dose ou interações), no máximo reformata/estrutura para os documentos. **Não aprovar** sem corrigir encerra o ciclo sem emitir documentos. Nada clínico é emitido sem aprovação/correção explícita do vet (RN-024/RN-096). Cada decisão é gravada (RN-098). | O que o vet escreve é o que vale; a IA não sobrescreve nem reinterpreta a decisão clínica.
- **RN-099.1 – Geração de documentos (Profissional+)** | Após a decisão, a IA gera **receita, prontuário estruturado e atestado a partir do estado final** (o que o vet aprovou ou reescreveu – RN-099). A geração é **formatação/estruturação, não nova inferência clínica**. No MVP a assinatura é o nome digitado (RN-031); não habilita dispensação externa de controlados (RN-091). | Documentos refletem exatamente o que o vet deixou como final.
- **RN-100 – Triagem de pré-sintomas (IA voltada ao Responsável)** | Etapa **distinta** da IA da consulta. Não é diagnóstico, exibe **disclaimer fixo**, e ante sinais de emergência **orienta atendimento presencial imediato** e destaca vets com disponibilidade imediata. | Segurança do animal e limite regulatório da IA voltada ao consumidor.

### I. Notificações – Matriz de Canais
- **RN-101** | Todo evento é classificado como **aviso** (pode ir por push/WhatsApp) ou **ação/conteúdo** (residente no app); a matriz (README.md §6.2) é a fonte de verdade. | Implementa a migração WhatsApp – App sem ambiguidade.
- **RN-102** | WhatsApp transporta exclusivamente lembretes e promoções; **nunca documentos, links de pagamento ou links de sala** – esses ficam no app. | Regra-mãe do papel do WhatsApp.
- **RN-103** | Promoções exigem opt-in de marketing específico e têm opt-out em 1 toque em toda mensagem. | LGPD + saúde do canal.
- **RN-104** | Régua de reenvio: 3 tentativas (push, push, WhatsApp-lembrete) + alerta de "Responsável não responsivo" (RN-030). No MVP implementa-se só push + inbox in-app; o degrau WhatsApp é alvo. | Reaproveitamento da régua base.

### J. Monetização de Dados – Nível 2 (Lead Qualificado)
- **RN-105** | Compartilhamento de dado individual **identificado** com parceiro (Lead Qualificado) só com **opt-in específico e destacado** e **opt-out em 1 toque**; cada compartilhamento é logado e visível ao Responsável. Não exercitado no MVP. | Onde mora o valor alto da monetização de dados – e só é legal com consentimento explícito.


---

## 5. Modelo de Dados

### 5.1 Entidades com atributos de estado
- **Responsável** – credenciais, dispositivos/push tokens, geolocalização (com fallback CEP/bairro – RN-047B), preferências de notificação, consentimentos granulares (5 finalidades, com data/hora e revogações), tier, saldo de pontos, saldo de créditos, contador de no-shows (90 dias).
- **Veterinário** – endereço embutido (CEP – UF, lat/long derivadas – RN-047A, modelo 1:1); nota média ponderada, nº de avaliações, taxa de conclusão, strikes ativos, status no matching, flag de patrocínio, tempo médio de resposta. *(Multi-unidade no futuro extrai Endereço para entidade 1:N; adiado por simplicidade.)*
- **Consulta** – pré-sintomas (estruturado + mídia), origem (app), estado (livre/em checkout/confirmada/realizada/cancelada/no-show – RN-058/RN-061), lock de checkout, contador de remarcações, status de no-show.
- **Animal** – calendário de ObrigaçõesDoPet; **peso** (obrigatório para dose – RN-096.2); flags de registros ocultados (exceto alertas de segurança – RN-088).
- **Documento** – destino app + banco do pet; assinatura (MVP: nome digitado; produção: ICP-Brasil – RN-031).
- **Pagamento** – registro simulado no MVP (status, valor, comissão RN-089, incidência do desconto RN-072); tipos consulta/marketplace(alvo)/crédito. Nenhum valor liquidado no MVP.

### 5.2 Entidades novas
| Entidade | Atributos principais | Relacionamentos | Escopo |
|---|---|---|---|
| **Avaliação** | nota geral (obrigatória), subnotas opcionais, comentário, data, status de moderação, resposta do vet (0..1), peso de recência | N:1 Consulta (única por consulta realizada – RN-076); N:1 Responsável; N:1 Veterinário | MVP |
| **ObrigaçãoDoPet** | tipo, data-limite, status | N:1 Animal; 0..1 Consulta que a cumpriu; gera Notificação e PontosFidelidade | MVP |
| **PontosFidelidade** | evento de origem, pontos, data, expiração (12m), estorno | N:1 Responsável; N:1 evento (Consulta/Obrigação) | MVP (acúmulo/tier reais; desconto calculado/exibido – RN-072) |
| **Notificação** | evento, classificação (aviso – ação/conteúdo), canal(is), tentativa da régua, status de leitura | N:1 Responsável (ou Vet); N:1 evento | MVP (push+inbox; WhatsApp alvo) |
| **LogDeAuditoriaIA** | timestamp, versão do modelo, tipo de sugestão, conteúdo sugerido, decisão, conteúdo final, CRMV | N:1 Consulta; N:1 Veterinário; imutável | MVP (IA real) |
| **LogDeAcessoProntuário** | quem, quando, contexto, base de acesso | N:1 Animal; N:1 Veterinário; visível ao Responsável | MVP |
| **Parceiro** | razão social, categorias, take rate, SLA | 1:N Produto; 1:N PedidoMarketplace | Alvo (mockado) |
| **PedidoMarketplace** | itens, valor, desconto, status, rastreio, ref. –  Receita | N:1 Responsável; N:1 Parceiro; 1:1 Pagamento; 0..1 Receita | Alvo (compra "em breve") |

---

## 6. Escopo do MVP – Mock vs. Real

Nenhuma regra é abandonada; os itens abaixo são **alvo de produção**, com a RN preservada.

- **Mapa visual da geolocalização** – fora do MVP; a busca é lista ordenada por distância (RN-047/047B). Mapa é evolução de produto.
- **Liquidação financeira real** (gateway, split, reembolso, NF fiscal) – RN-012/037/089: calculado e registrado, não liquidado.
- **Marketplace real** (Parceiro, PedidoMarketplace, recompra, take rate) – RN-090/091/073: mockado, compra "em breve".
- **Canal WhatsApp** (lembretes/promoções + degrau da régua) – RN-102/104: só push + in-app no MVP.
- **Assinatura ICP-Brasil** – RN-031: no MVP, nome digitado; dispensação externa de controlados bloqueada até a certificação.
- **Monetização de dados** (Níveis 1 e 2) – RN-093/105
- **Integrações Enterprise** (labs, NFS-e, planos, ERPs, farmácias, calendários) – README.md §4.3: só documentadas.
- **Emergência / SOS** – fora do escopo, versão futura.

---

*Documento técnico do Vetly. Companheiro de [README.md](./README.md) (produto e fluxos). Toda RN é rastreável; onde é alvo de produção e não MVP, está sinalizado sem apagar a regra.*
