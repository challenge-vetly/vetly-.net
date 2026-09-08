# Roteiro de testes no Postman

Jornada completa da API, na ordem em que funciona. Cada passo traz **método, rota, uma linha de descrição e o corpo pronto para copiar e colar**.

É o mesmo caminho que o teste de integração `JornadaCompletaTests` percorre por HTTP.

---

## Preparação

### Variáveis de ambiente

Crie um *Environment* no Postman com estas variáveis. Só `baseUrl` e `tokenInterno` precisam de valor inicial — o resto é preenchido pelos scripts de cada passo.

| Variável | Valor inicial |
|---|---|
| `baseUrl` | `https://localhost:7262` |
| `tokenInterno` | o mesmo valor de `Servicos:TokenInterno` do seu `appsettings.Development.local.json` |
| `tokenAdmin` · `tokenVet` · `tokenTutor` | *(vazio)* |
| `vetId` · `tutorId` · `animalId` · `servicoId` · `slotId` | *(vazio)* |
| `consultaId` · `pagamentoId` · `referenciaExterna` · `documentoId` · `avaliacaoId` | *(vazio)* |

> **Certificado local.** Em *Settings → General*, desligue **SSL certificate verification**. O certificado de desenvolvimento é autoassinado.

### Cabeçalhos

| Cabeçalho | Quando |
|---|---|
| `Content-Type: application/json` | todo `POST`, `PUT` e `PATCH` com corpo |
| `Authorization: Bearer {{tokenTutor}}` | conforme o perfil indicado em cada passo |
| `Idempotency-Key: {{$guid}}` | **obrigatório** nas 6 rotas marcadas com 🔑 |
| `X-Vetly-Service-Token: {{tokenInterno}}` | apenas nas rotas `/api/internos/*` |

`{{$guid}}` é uma variável dinâmica do Postman: gera um GUID novo a cada envio, que é exatamente o comportamento esperado da chave de idempotência.

**As 6 rotas que exigem `Idempotency-Key`:**

`POST /api/consultas/checkout` · `DELETE /api/consultas/{id}` · `POST /api/consultas/{id}/remarcar` · `POST /api/consultas/{id}/retorno` · `POST /api/pagamentos` · `POST /api/fidelidade/resgates`

### Enums viajam como texto

O JSON usa **strings**, não números: `"plano": "Profissional"`, nunca `"plano": 2`. Os valores aceitos estão no [apêndice](#apêndice--valores-de-enum).

### Antes de começar: popule a tabela de CEP

A geocodificação resolve coordenadas contra a tabela `TB_CEP_COORDENADA`, que **nasce vazia**. Sem ela o veterinário fica sem posição e a busca do passo 3.1 devolve lista vazia — sem erro, apenas nada. Rode no Oracle antes do roteiro:

```sql
INSERT INTO TB_CEP_COORDENADA (CEP, LATITUDE, LONGITUDE, CIDADE, UF)
  VALUES ('01310100', -23.561414, -46.655881, 'Sao Paulo', 'SP');
INSERT INTO TB_CEP_COORDENADA (CEP, LATITUDE, LONGITUDE, CIDADE, UF)
  VALUES ('04538133', -23.594290, -46.685150, 'Sao Paulo', 'SP');
COMMIT;
```

O CEP é gravado **sem máscara**, com 8 dígitos.

---

## Fluxo 1 — Preparar o prestador

Perfil: **Admin**, depois **Veterinário**.

### 1.1 · Token de Admin

Há dois caminhos, e o certo depende do ambiente.

**Em `Development`** — `POST {{baseUrl}}/api/auth/token` emite um JWT de Admin sem senha:

```json
{
  "usuario": "admin-teste",
  "role": "Admin"
}
```

**Em qualquer outro ambiente essa rota responde 404**, e é deliberado: emitir token sem credencial em produção seria porta aberta. Lá o Admin faz login normal, como qualquer usuário:

`POST {{baseUrl}}/api/auth/login`

```json
{
  "email": "admin@vetly.com.br",
  "senha": "SUA_SENHA"
}
```

A role `Admin` **não** vem de um cadastro separado: ela é derivada de o veterinário administrar alguma empresa (`Empresa.AdministradorId`, §4.1). O primeiro administrador é criado no arranque pelo [SemeadorDoAdministrador](src/Vetly.API/Jobs/SemeadorDoAdministrador.cs), a partir de `Bootstrap__AdminEmail` — ver a seção de Deploy do [README](README.md#7-deploy).

Confira `"role": "Admin"` na resposta. Se vier `"Veterinario"`, o profissional não administra unidade nenhuma e as rotas de administração vão responder 403.

```js
// Aba Tests — serve para os dois caminhos
pm.environment.set("tokenAdmin", pm.response.json().token);
```

### 1.2 · Cadastrar o veterinário

`POST {{baseUrl}}/api/veterinarios` · Bearer `{{tokenAdmin}}`

Cria o profissional, valida o CRMV junto ao conselho e devolve a **senha temporária**, exibida uma única vez.

```json
{
  "nome": "Marina Alves",
  "crmv": "12345-SP",
  "ufAtuacao": "SP",
  "email": "marina@clinica.com",
  "persona": "Autonomo",
  "plano": "Profissional",
  "especialidades": ["Clinica Geral"],
  "especiesAtendidas": ["Canino", "Felino"],
  "endereco": {
    "cep": "01310-100",
    "logradouro": "Avenida Paulista",
    "numero": "1000",
    "bairro": "Bela Vista",
    "cidade": "Sao Paulo",
    "uf": "SP"
  }
}
```

```js
pm.environment.set("vetId", pm.response.json().id);
```

> `latitude` e `longitude` são ignoradas na entrada — o servidor as deriva do endereço (RN-026). Por isso o passo do CEP importa.

### 1.3 · Login do veterinário

`POST {{baseUrl}}/api/auth/login`

Autentica com a senha temporária devolvida em 1.2. A resposta traz `senhaTemporaria: true`.

```json
{
  "email": "marina@clinica.com",
  "senha": "COLE_A_SENHA_TEMPORARIA_DO_PASSO_1.2"
}
```

```js
pm.environment.set("tokenVet", pm.response.json().token);
```

### 1.4 · Configurar a agenda

`PUT {{baseUrl}}/api/veterinarios/{{vetId}}/agenda-config` · Bearer `{{tokenVet}}`

Define a jornada e **materializa 60 dias de horários**. Sem esta chamada não existe horário para o Responsável escolher.

```json
{
  "dias": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
  "horaInicio": "08:00",
  "horaFim": "18:00",
  "duracaoMinutos": 30,
  "intervaloMinutos": 0
}
```

### 1.5 · Definir os serviços

`PUT {{baseUrl}}/api/veterinarios/{{vetId}}/servicos` · Bearer `{{tokenVet}}`

A vitrine com preço e duração. **É daqui que sai o valor cobrado** — nunca do corpo do pagamento.

```json
{
  "servicos": [
    { "tipo": "ConsultaRotina", "valor": 200.00, "duracaoMinutos": 30, "aceitaPlanoPet": false },
    { "tipo": "Retorno",        "valor": 0.00,   "duracaoMinutos": 30, "aceitaPlanoPet": false }
  ]
}
```

```js
pm.environment.set("servicoId", pm.response.json()[0].id);
```

### 1.6 · Cadastrar a conta de repasse

`PUT {{baseUrl}}/api/veterinarios/me/dados-repasse` · Bearer `{{tokenVet}}`

Fecha o onboarding financeiro (§4.1). **Não há id na rota**: a conta é sempre a de quem
está com o token, porque a §7.3 veda ao administrador da unidade os dados bancários
pessoais dos vinculados — com um `Guid` aqui, bastaria trocá-lo.

```json
{
  "banco": "341",
  "agencia": "1234",
  "conta": "0012345-6",
  "documentoTitular": "123.456.789-09",
  "chavePix": "marina@clinicavetly.com.br"
}
```

O documento aceita CPF ou CNPJ, com ou sem pontuação, e é gravado só com os dígitos.
Comprimento diferente de 11 ou 14 devolve **400**.

`GET` na mesma rota devolve a conta com o número e a chave Pix **mascarados** — a tela
de conferência precisa dos últimos dígitos, não do número inteiro. Antes do cadastro,
responde `200` com `"configurado": false`, e não 404: o veterinário existe, o que falta
é um passo do onboarding.

Para a **empresa**, a rota equivalente é `PUT /api/empresas/{{empresaId}}/dados-repasse`
com token de Admin: é a conta do estabelecimento, que é quem recebe o repasse quando o
vet é vinculado.

---

## Fluxo 2 — Onboarding do Responsável

Perfil: **Tutor**.

### 2.1 · Criar a conta

`POST {{baseUrl}}/api/auth/registro/tutor`

Cadastro público. Devolve a sessão já com `consentimentoPendente: true`.

```json
{
  "nome": "Ana Souza",
  "email": "ana@exemplo.com",
  "telefone": "11999998888",
  "senha": "senha-forte-123"
}
```

```js
const r = pm.response.json();
pm.environment.set("tokenTutor", r.token);
pm.environment.set("tutorId", r.tutorId);
```

### 2.2 · Conceder o consentimento

`PUT {{baseUrl}}/api/tutores/{{tutorId}}/consentimentos` · Bearer `{{tokenTutor}}`

**Sem isto, toda rota de negócio responde 422.** A base legal precede o tratamento dos dados (RN-060).

```json
{
  "consentimentos": [
    { "finalidade": "Atendimento", "concedido": true },
    { "finalidade": "Lembretes", "concedido": true },
    { "finalidade": "Compartilhamento", "concedido": true }
  ]
}
```

### 2.3 · Cadastrar o animal

`POST {{baseUrl}}/api/animais` · Bearer `{{tokenTutor}}`

`pesoKg` é obrigatório — sem peso a IA não sugere dose. A carteira informada já deriva o calendário de obrigações.

```json
{
  "nome": "Thor",
  "especie": "Canino",
  "raca": "Golden Retriever",
  "dataNascimento": "2023-04-10T00:00:00Z",
  "tutorId": "{{tutorId}}",
  "pesoKg": 31.5,
  "sexo": "Macho",
  "castrado": true,
  "alergias": ["Dipirona"],
  "condicoesPreexistentes": [],
  "carteiraVacinacao": [
    { "tipo": "V10", "aplicadaEm": "2025-03-15T00:00:00Z" }
  ]
}
```

```js
pm.environment.set("animalId", pm.response.json().id);
```

### 2.4 · Registrar o aparelho para push

`POST {{baseUrl}}/api/tutores/{{tutorId}}/dispositivos` · Bearer `{{tokenTutor}}`

Idempotente por token de push: reinstalar o app reaproveita o registro.

```json
{
  "pushToken": "fcm-token-de-teste-0001",
  "plataforma": "Android"
}
```

---

## Fluxo 3 — Buscar e agendar

### 3.1 · Buscar prestadores

`GET {{baseUrl}}/api/busca?animalId={{animalId}}&lat=-23.561414&lng=-46.655881&raioKm=10` · Bearer `{{tokenTutor}}`

Lista por proximidade e necessidade, ordenada pelo score 40/30/30. A espécie do animal é filtro **eliminatório**.

| Query | Uso |
|---|---|
| `animalId` | **obrigatório** |
| `lat` + `lng` | posição vinda do GPS |
| `cep` | alternativa ao GPS |
| `raioKm` | padrão 10, máximo 25 |
| `necessidade` | filtra por tipo de serviço |
| `atendeHoje` | `true` para só quem tem horário livre hoje |

> Lista vazia é o sintoma de coordenada ausente — confira se o CEP do veterinário existe em `TB_CEP_COORDENADA`.

### 3.2 · Ver horários livres

`GET {{baseUrl}}/api/veterinarios/{{vetId}}/disponibilidade` · Bearer `{{tokenTutor}}`

Horários livres agrupados por dia.

```js
pm.environment.set("slotId", pm.response.json().dias[0].horarios[0].id);
```

### 3.3 · Checkout 🔑

`POST {{baseUrl}}/api/consultas/checkout` · Bearer `{{tokenTutor}}` · `Idempotency-Key: {{$guid}}`

**Trava o horário por 10 minutos** e cria a consulta em `EmCheckout`. Quem chegar depois recebe 409.

```json
{
  "animalId": "{{animalId}}",
  "prestadorId": "{{vetId}}",
  "slotId": "{{slotId}}",
  "servicoId": "{{servicoId}}"
}
```

```js
pm.environment.set("consultaId", pm.response.json().consultaId);
```

### 3.4 · Criar a cobrança 🔑

`POST {{baseUrl}}/api/pagamentos` · Bearer `{{tokenTutor}}` · `Idempotency-Key: {{$guid}}`

Cria a cobrança com o split já apurado. Responde **202** e fica pendente — quem confirma é o webhook.

```json
{
  "tutorId": "{{tutorId}}",
  "consultaId": "{{consultaId}}",
  "valor": 200.00,
  "meioPagamento": "Pix"
}
```

```js
const r = pm.response.json();
pm.environment.set("pagamentoId", r.id);
pm.environment.set("referenciaExterna", r.instrucoes.referenciaExterna);
```

> **O `valor` do corpo é ignorado.** O preço cobrado sai de `Servico.Valor`. Mande `1.00` e confira: a resposta volta com `200.00`.

### 3.5 · Confirmar o pagamento (webhook)

`POST {{baseUrl}}/api/internos/pagamentos/webhook` · Header `X-Vetly-Service-Token: {{tokenInterno}}`

Simula o provedor confirmando. **Promove a consulta de `EmCheckout` para `Confirmada`.** Não usa JWT.

```json
{
  "referenciaExterna": "{{referenciaExterna}}",
  "status": "Confirmado"
}
```

Troque para `"status": "Recusado"` para ver o caminho inverso: a consulta expira e o horário volta a ficar livre.

### 3.6 · Descrever os sintomas

`PUT {{baseUrl}}/api/consultas/{{consultaId}}/pre-sintomas` · Bearer `{{tokenTutor}}`

O relato de quem convive com o animal. Alimenta o briefing do veterinário.

```json
{
  "queixaPrincipal": "Vomito ha dois dias",
  "duracaoEmDias": 2,
  "sinaisObservados": ["Apatia", "Recusa alimentar"],
  "alimentacaoNormal": false,
  "mudancaDeComportamento": true,
  "observacoes": "Bebeu pouca agua ontem."
}
```

---

## Fluxo 4 — O atendimento

Perfil: **Veterinário** (`{{tokenVet}}`) em todos os passos.

### 4.1 · Painel do dia

`GET {{baseUrl}}/api/dashboard/veterinario`

Agenda do dia e pendências que travam dinheiro ou documento. Sem id na rota — o escopo vem do token.

### 4.2 · Briefing antes de atender

`GET {{baseUrl}}/api/consultas/{{consultaId}}/briefing`

Histórico, alergias, peso, medicações e os pré-sintomas já organizados.

### 4.3 · Iniciar a consulta

`POST {{baseUrl}}/api/consultas/{{consultaId}}/iniciar`

**Abre a janela de captura.** Devolve os avisos que o profissional precisa ver antes, como peso ausente. Sem corpo.

### 4.4 · Enviar um trecho de áudio *(opcional)*

`POST {{baseUrl}}/api/consultas/{{consultaId}}/captura/segmentos`

Enfileira a transcrição fora da requisição e responde 202. O áudio já deve estar no storage — aqui viaja só o `midiaId`. Disponível nos planos Profissional e Enterprise.

```json
{
  "sequencia": 0,
  "midiaId": "COLE_O_ID_DA_MIDIA",
  "duracaoMs": 30000,
  "inicioRelativoMs": 0
}
```

Para obter um `midiaId`: `POST /api/midia/upload-url` devolve a URL assinada, você faz o `PUT` do arquivo nela, e o id volta na resposta.

### 4.5 · Acompanhar a captura

`GET {{baseUrl}}/api/consultas/{{consultaId}}/captura`

Situação da sessão e o texto já transcrito.

### 4.6 · Encerrar a consulta

`POST {{baseUrl}}/api/consultas/{{consultaId}}/encerrar`

**Fecha a janela** e marca a consulta como `Realizada`. Encerrar não é finalizar. Sem corpo.

### 4.7 · Ler o rascunho da IA

`GET {{baseUrl}}/api/consultas/{{consultaId}}/rascunho`

O prontuário que a IA estruturou a partir da transcrição. Responde 404 enquanto a estruturação não terminou.

### 4.8 · Decidir sobre o rascunho

`PUT {{baseUrl}}/api/consultas/{{consultaId}}/validar-diagnostico`

A decisão do veterinário. **Não há aprovação por omissão** — e a decisão vira registro append-only.

```json
{
  "decisao": "Aprovado"
}
```

Corrigindo o conteúdo:

```json
{
  "decisao": "Corrigido",
  "correcao": {
    "anamnese": "Vomito ha dois dias, sem diarreia.",
    "exameFisico": "Hidratado, mucosas normocoradas, abdome indolor a palpacao.",
    "hipotesesDiagnosticas": ["Gastrite alimentar"],
    "conduta": "Dieta branda por cinco dias.",
    "orientacoes": "Retornar se o vomito persistir."
  }
}
```

Recusando:

```json
{
  "decisao": "NaoAprovado",
  "justificativa": "O rascunho nao reflete o exame fisico realizado."
}
```

> `NaoAprovado` **não** valida o diagnóstico — e sem diagnóstico validado não se gera documento. O caminho segue pelo passo 4.9.

### 4.9 · Prontuário manual

`POST {{baseUrl}}/api/consultas/{{consultaId}}/prontuario-manual`

O caminho sem IA: plano Básico, falha da transcrição ou rascunho recusado. Havendo rascunho ainda pendente, responde 409.

```json
{
  "conteudo": {
    "anamnese": "Vomito ha dois dias, sem diarreia.",
    "exameFisico": "Hidratado, mucosas normocoradas, abdome indolor a palpacao.",
    "hipotesesDiagnosticas": ["Gastrite alimentar"],
    "conduta": "Dieta branda por cinco dias.",
    "orientacoes": "Retornar se o vomito persistir."
  }
}
```

### 4.10 · Conferir a trilha de auditoria

`GET {{baseUrl}}/api/consultas/{{consultaId}}/auditoria-ia`

Append-only: registra cada decisão com o conteúdo final, quem decidiu e qual modelo. A mesma consulta pode acumular decisões — a recusa e, depois, o prontuário manual que a sucede.

---

## Fluxo 5 — Documentos

Perfil: **Veterinário**, exceto onde indicado.

### 5.1 · Gerar o documento

`POST {{baseUrl}}/api/documentos/consulta/{{consultaId}}?tipo=Prontuario`

Gera pela Factory do tipo, com conteúdo e PDF. Exige diagnóstico validado. Sem corpo.

| `tipo` | Observação |
|---|---|
| `Prontuario` | — |
| `ReceitaVeterinaria` | exige assinatura antes de publicar |
| `Atestado` | acrescente `&subtipo=Saude`, `Obito` ou `Vacinacao` |
| `NotaFiscal` | exige pagamento confirmado |

```js
pm.environment.set("documentoId", pm.response.json().id);
```

### 5.2 · Assinar

`POST {{baseUrl}}/api/documentos/{{documentoId}}/assinar`

O nome digitado é conferido contra o nome registrado. Só o veterinário do atendimento assina.

```json
{
  "nomeCompleto": "Marina Alves"
}
```

### 5.3 · Publicar

`POST {{baseUrl}}/api/documentos/{{documentoId}}/publicar`

Publica no board do pet e notifica o Responsável. **Receita sem assinatura não é publicada.** Sem corpo.

### 5.4 · Finalizar a consulta

`POST {{baseUrl}}/api/consultas/{{consultaId}}/finalizar`

Fecho documental. Exige que todo documento já emitido que precise de assinatura esteja assinado. Devolve `estadoDaSessao: "Concluida"`. Sem corpo.

### 5.5 · Board do pet *(Tutor)*

`GET {{baseUrl}}/api/documentos/animal/{{animalId}}` · Bearer `{{tokenTutor}}`

Os documentos publicados do animal, na visão do Responsável.

### 5.6 · Marcar como lido *(Tutor)*

`POST {{baseUrl}}/api/documentos/{{documentoId}}/lido` · Bearer `{{tokenTutor}}`

Registra que o Responsável abriu o documento. Sem corpo.

### 5.7 · Corrigir um documento

`POST {{baseUrl}}/api/documentos/{{documentoId}}/correcao`

Cria uma **nova versão** — o original é preservado. Depois de 24h a justificativa passa a ser obrigatória.

```json
{
  "novosDados": "Conteudo corrigido do documento.",
  "justificativa": "Correcao da dosagem prescrita.",
}
```

---

## Fluxo 6 — Depois da consulta

### 6.1 · Avaliar o atendimento *(Tutor)*

`POST {{baseUrl}}/api/avaliacoes/consulta/{{consultaId}}` · Bearer `{{tokenTutor}}`

Só quem foi atendido avalia, uma vez por consulta e em até 14 dias.

```json
{
  "nota": 5,
  "comentario": "Atendimento atencioso e explicacao clara."
}
```

```js
pm.environment.set("avaliacaoId", pm.response.json().id);
```

### 6.2 · Ver a reputação

`GET {{baseUrl}}/api/avaliacoes/veterinario/{{vetId}}`

Nota média e distribuição. Abaixo de 3 avaliações a nota não é pública nem entra no score de busca — confira `notaPublica: false`.

### 6.3 · Responder à avaliação *(Vet)*

`POST {{baseUrl}}/api/avaliacoes/{{avaliacaoId}}/resposta` · Bearer `{{tokenVet}}`

A resposta pública do veterinário — uma só.

```json
{
  "resposta": "Obrigada pela confianca! Qualquer duvida, estamos a disposicao."
}
```

### 6.4 · Saldo de pontos *(Tutor)*

`GET {{baseUrl}}/api/fidelidade/saldo` · Bearer `{{tokenTutor}}`

Saldo, tier, multiplicador e o que vence em 30 dias. Sem id na rota — o escopo vem do token.

### 6.5 · Simular um resgate *(Tutor)*

`POST {{baseUrl}}/api/fidelidade/resgates/simular` · Bearer `{{tokenTutor}}`

Mostra o desconto em reais e como o custo se divide entre Vetly e prestador. **Não grava nada.**

```json
{
  "itemRef": "racao-premium-2kg",
  "itemNome": "Racao Premium 2kg",
  "categoria": "Alimentacao",
  "pontos": 200
}
```

### 6.6 · Resgatar 🔑 *(Tutor)*

`POST {{baseUrl}}/api/fidelidade/resgates` · Bearer `{{tokenTutor}}` · `Idempotency-Key: {{$guid}}`

Debita os pontos em FIFO e emite o cupom com QR e 30 dias de validade. Mesmo corpo do passo 6.5.

### 6.7 · Caixa de entrada *(Tutor)*

`GET {{baseUrl}}/api/notificacoes/tutor/{{tutorId}}` · Bearer `{{tokenTutor}}`

A notificação é gravada antes de ser enviada — a caixa sobrevive ao push perdido.

### 6.8 · Board do pet *(Tutor)*

`GET {{baseUrl}}/api/animais/{{animalId}}/board` · Bearer `{{tokenTutor}}`

Obrigações, agendamentos, documentos e o estado do avatar.

### 6.9 · Agendar o retorno 🔑 *(Vet)*

`POST {{baseUrl}}/api/consultas/{{consultaId}}/retorno` · Bearer `{{tokenVet}}` · `Idempotency-Key: {{$guid}}`

Nasce **confirmado e sem cobrança nova** — é a segunda metade de um tratamento já pago.

```json
{
  "slotId": "COLE_UM_SLOT_LIVRE",
  "motivo": "Reavaliacao apos cinco dias de dieta."
}
```

---

## Fluxo 7 — Cenários que valem testar

Depois do caminho feliz, estes são os que mostram as regras funcionando.

| # | Requisição | Resultado esperado |
|---|---|---|
| 7.1 | `GET /api/consultas/{{consultaId}}/simulacao-cancelamento` | O valor do reembolso **antes** de cancelar, pela mesma Strategy que o cancelamento usa |
| 7.2 | `DELETE /api/consultas/{{consultaId}}` 🔑 | Aplica a faixa por antecedência: >24h integral · 2h–24h parcial · <2h sem reembolso |
| 7.3 | `POST /api/consultas/{{consultaId}}/remarcar` 🔑 com `{"novoSlotId":"..."}` | Transfere horário e pagamento. **Limite de 2** — a terceira responde 422 |
| 7.4 | `POST /api/consultas/checkout` 🔑 **duas vezes no mesmo `slotId`** | A segunda recebe **409**. A concorrência é resolvida no banco, não em memória |
| 7.5 | `POST /api/pagamentos` 🔑 **repetindo o mesmo `Idempotency-Key`** | Devolve a **mesma resposta**, sem criar uma segunda cobrança |
| 7.6 | `GET /api/consultas/{{consultaId}}` com o token de **outro** Responsável | **403** com `"codigo": "RN-105"`. O escopo vem do token, nunca de parâmetro |
| 7.7 | Qualquer rota de negócio **sem o consentimento do passo 2.2** | **422** com `"codigo": "RN-060"` |
| 7.8 | `POST /api/consultas/checkout` **sem** `Idempotency-Key` | **400** — o cabeçalho é exigido nas 6 rotas marcadas |
| 7.9 | `POST /api/documentos/consulta/{id}?tipo=ReceitaVeterinaria` seguido de `publicar` **sem assinar** | Recusa a publicação: no board a receita pareceria válida sem ser |
| 7.10 | `GET /api/financeiro/consolidado` *(Admin)* | O campo `fecha` confirma `bruto = comissão + repasse + desconto` |
| 7.11 | `POST /api/colmeia` *(Tutor)* | Autoriza um vet de fora a alcançar o histórico, com escopo e prazo |
| 7.12 | `GET /api/animais/{{animalId}}/acessos` *(Tutor)* | A trilha append-only de todo acesso ao histórico — permitido **ou** negado |
| 7.13 | `GET /api/notificacoes/tutor/{{tutorId}}` *(Tutor)*, logo após o passo 3.5 | A confirmação do agendamento (`ConsultaConfirmada`) está lá. O aviso nasce no **webhook**, não na resposta da cobrança |
| 7.14 | O mesmo, depois do 7.2 | `ReembolsoConfirmado` quando quem cancelou foi o Responsável; `CancelamentoPeloPrestador` quando foi a operação. Cancelamento sem reembolso também avisa |
| 7.15 | O mesmo, depois de a consulta ser realizada e o job de pontos rodar | `PontosCreditados`. O título anuncia o tier só quando a faixa muda |
| 7.16 | `GET /api/dashboard/unidade` *(Admin da unidade)* | Agenda de todos os vinculados no dia, ocupação e `responsaveisNaoResponsivos`. **Sem id na rota** — a unidade sai do vínculo do próprio Admin (§7.3) |
| 7.17 | `GET /api/dashboard/unidade` com token de **Vet** | **403**. O painel da unidade é da administração |
| 7.18 | `GET /api/veterinarios/me/dados-repasse` com token de **Admin sem cadastro profissional** | **403**. Não existe `Guid` na rota para o Admin trocar (§7.3) |

Corpo do 7.11:

```json
{
  "animalId": "{{animalId}}",
  "veterinarioId": "COLE_O_ID_DE_OUTRO_VETERINARIO",
  "escopo": "HistoricoCompleto",
  "validadeEmDias": 30,
  "motivo": "Atendimento de emergencia em outra clinica."
}
```

---

## Fluxo 8 — Observabilidade

Sem autenticação.

| # | Requisição | O que mostra |
|---|---|---|
| 8.1 | `GET {{baseUrl}}/health/live` | O processo está no ar. Não toca dependência nenhuma |
| 8.2 | `GET {{baseUrl}}/health/ready` | As dependências respondem. 503 tira a instância de rotação |
| 8.3 | `GET {{baseUrl}}/health` | Relatório completo, com duração e tags de cada check |
| 8.4 | `GET {{baseUrl}}/metrics` | Métricas Prometheus. Procure `vetly_http_requisicoes_total` e `vetly_regras_violadas_total` |

**Teste de correlação.** Envie qualquer requisição com o cabeçalho `X-Correlation-Id: meu-teste-123`. A resposta volta com o mesmo valor; se falhar, o corpo do erro traz `"correlationId": "meu-teste-123"`; e `grep meu-teste-123 src/Vetly.API/logs/vetly-*.log` mostra todas as linhas daquela requisição, de todas as camadas.

---

## Apêndice — valores de enum

| Enum | Valores |
|---|---|
| `persona` | `Autonomo` · `Vinculado` |
| `plano` | `Basico` · `Profissional` · `Enterprise` |
| `sexo` | `Macho` · `Femea` |
| `finalidade` | `Atendimento` · `Lembretes` · `Compartilhamento` · `Promocoes` · `DadosAgregados` |
| `meioPagamento` | `Pix` · `CartaoCredito` · `CartaoDebito` · `Dinheiro` · `Boleto` |
| `tipo` (serviço) | `ConsultaRotina` · `Emergencia` · `Vacinacao` · `Exame` · `Cirurgia` · `Banho` · `Tosa` · `Retorno` |
| `tipo` (documento) | `Prontuario` · `ReceitaVeterinaria` · `Atestado` · `NotaFiscal` |
| `subtipo` (atestado) | `Saude` · `Obito` · `Vacinacao` |
| `decisao` | `Aprovado` · `Corrigido` · `NaoAprovado` |
| `escopo` (colmeia) | `HistoricoCompleto` · `UltimaConsulta` · `Documentos` |
| `categoria` (resgate) | `Alimentacao` · `Medicamentos` · `Higiene` |
| `tipo` (obrigação) | `Vacina` · `Vermifugo` · `Antiparasitario` · `Retorno` · `CheckUp` · `MedicacaoContinua` · `Exame` |
| `plataforma` | `Android` · `Ios` |
| `dias` (agenda) | `Sunday` … `Saturday` — é o `DayOfWeek` do .NET, em inglês |
| `status` (webhook) | `Confirmado` · `Recusado` |

---

Todos os 152 endpoints são exercitados contra um ambiente real por [deploy/validar-endpoints.py](deploy/validar-endpoints.py).

Documentação interativa: **`https://localhost:7262/scalar/v1`**.
Regras de negócio por código: [REGRAS-DE-NEGOCIO.md](REGRAS-DE-NEGOCIO.md).
