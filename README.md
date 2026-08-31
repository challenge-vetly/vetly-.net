# Vetly API — Detalhamento do Projeto

O Vetly é uma API REST para gestão de clínicas veterinárias, cobrindo todo o ciclo do atendimento — agendamento de consultas com pagamento integrado, prontuários, internações com apuração financeira, exames, emissão de documentos clínicos (prontuário, receita, atestado, nota fiscal) e split financeiro entre profissionais e empresas. Integra um assistente de IA local (Ollama) para sugerir hipóteses diagnósticas, protocolos de tratamento, triagem de sintomas e orientações pós-atendimento, sempre validados manualmente pelo veterinário (RN-082).

## Stack

| Camada | Tecnologia |
|---|---|
| Framework | ASP.NET Core 10 (Web API) |
| ORM | EF Core 10 + Oracle.EntityFrameworkCore |
| Banco | Oracle Database 21c+ |
| Autenticação | JWT Bearer |
| Documentação | Scalar (tema DeepSpace) em `/scalar/v1` |
| IA | Ollama local (modelo `llama3.1`) |
| Testes | xUnit + Moq (630 testes verdes) |

## Padrões aplicados

| Padrão | Onde |
|---|---|
| Factory Pattern | `DocumentoService` seleciona `IDocumentoFactory` pelo `TipoDocumento` (Prontuario, Receita, Atestado, NotaFiscal) |
| Strategy Pattern | `ConsultaService` seleciona `ICancelamentoStrategy` por antecedência (RN-014/RN-041/RN-042) |
| Strategy Pattern | `PagamentoService` seleciona `ISplitFinanceiroStrategy` pelo **plano** (Básico 15% / Profissional 12% / Enterprise 10%) |
| Repository Pattern | Interfaces em `Vetly.Application`; implementações EF Core em `Vetly.Infrastructure` |
| DIP | Todos os serviços dependem de interfaces — zero acoplamento concreto |
| Soft Delete | `Veterinario`, `Animal` e `Tutor` são desativados, nunca deletados |
| Value Object | `Crmv` — imutável, valida regex `^\d{4,6}-[A-Z]{2}$` |
| ProblemDetails | `ExceptionHandlingMiddleware` retorna RFC 7807 em todos os erros |
| Enums como string | `JsonStringEnumConverter` — o JSON trafega `"Presencial"`, não `1` (entrada e saída) |
| Worker de negócio | `VetlyBackgroundService` + `TB_JOB`: rotinas periódicas (expirar locks, limpar idempotência) e jobs pontuais (promover lista de espera, webhook simulado) |
| Idempotência | `IdempotencyFilter` + `TB_IDEMPOTENCIA`: rotas marcadas com `[Idempotente]` exigem `Idempotency-Key` e reaproveitam a resposta por 24h |
| Adapter / Port | Dependências externas entram por porta na `Application` e implementação `*Simulado` na `Infrastructure`, escolhida por configuração (`Adaptadores:*`) — trocar de fornecedor é trocar o registro no DI |

---

## Instalação e como rodar

**Pré-requisitos:**

- .NET 10 SDK
- Oracle Database 21c+ acessível (a connection string padrão aponta para `oracle.fiap.com.br:1521/orcl`)
- Ollama instalado e rodando — baixar em https://ollama.com/download, depois:

```bash
ollama serve
ollama pull llama3.1
```

Validar:

```bash
curl -X POST http://localhost:11434/api/generate \
  -H "Content-Type: application/json" \
  -d '{"model":"llama3.1","prompt":"ola","stream":false}'
```

**Configuração** — crie o arquivo `src/Vetly.API/appsettings.Development.local.json` com suas credenciais Oracle:

```json
{
  "ConnectionStrings": {
    "OracleConnection": "User Id=SEU_USUARIO;Password=SUA_SENHA;Data Source=oracle.fiap.com.br:1521/orcl"
  },
  "Jwt": {
    "Key": "VetlySecretKey_MustBeAtLeast32CharactersLong!"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama3.1",
    "TimeoutSeconds": 120
  }
}
```

> Este arquivo está no `.gitignore` e **não é commitado**. Substitua `SEU_USUARIO` e `SUA_SENHA` pelas suas credenciais Oracle. O modelo Ollama utilizado no projeto é o `llama3.1` — certifique-se de tê-lo instalado com `ollama pull llama3.1`.

**Rodar:**

```bash
# 1. Restaurar e compilar
dotnet restore
dotnet build

# 2. Aplicar migrations no Oracle
dotnet ef database update --project src/Vetly.Infrastructure --startup-project src/Vetly.API

# 3. Subir a API (HTTPS na porta 7262)
dotnet run --project src/Vetly.API --launch-profile https

# 4. Abrir documentação interativa
# https://localhost:7262/scalar/v1

# 5. Rodar os testes
dotnet test
```

---

## Como autenticar

Todos os endpoints exigem JWT, exceto as rotas públicas de `api/auth` e os health checks.

**Responsável (app)** — cadastro e login com e-mail e senha:

```bash
# 1. Cadastro — devolve token, refreshToken e consentimentoPendente
curl -X POST https://localhost:7262/api/auth/registro/tutor -H "Content-Type: application/json" -d '{"nome":"Ana","email":"ana@exemplo.com","telefone":"11999998888","senha":"senha-forte-123"}'

# 2. Login nas próximas vezes
curl -X POST https://localhost:7262/api/auth/login -H "Content-Type: application/json" -d '{"email":"ana@exemplo.com","senha":"senha-forte-123"}'

# 3. Renovação — o refresh token rotaciona a cada uso
curl -X POST https://localhost:7262/api/auth/refresh -H "Content-Type: application/json" -d '{"refreshToken":"..."}'
```

O token de acesso vale 8 horas; o refresh token, 30 dias, e é **rotativo**: cada renovação revoga o anterior. Reapresentar um token já usado derruba todas as sessões daquele usuário — é o sinal de que ele vazou.

**Veterinário** — o Admin cadastra o profissional em `POST /api/veterinarios` e a resposta traz a **senha temporária** de primeiro acesso, exibida uma única vez. O veterinário entra em `/api/auth/login` com ela e troca em `POST /api/auth/trocar-senha`. Vet desativado ainda faz login, mas com a role `VetDesativado`, limitada ao extrato dos próprios atendimentos (RN-022/RN-024).

**Admin (desenvolvimento)** — o Admin ainda não tem cadastro próprio; a rota obsoleta segue disponível apenas em `Development`:

```bash
curl -X POST https://localhost:7262/api/auth/token -H "Content-Type: application/json" -d '{"usuario":"admin-teste","role":"Admin"}'
```

Roles: `Tutor`, `Veterinario` e `Admin`. Policies: `ApenasAdmin`, `VeterinarioOuAdmin`, `ApenasTutor` e `TutorOuAdmin`. Use o token com `Authorization: Bearer {token}`.

As senhas são guardadas com PBKDF2-HMAC-SHA256, 210.000 iterações e salt aleatório por senha, no formato autodescritivo `pbkdf2$sha256$iteracoes$salt$hash` — aumentar o custo no futuro não invalida as senhas já cadastradas.
---

## Endpoints

### Health Checks
Públicos (sem autenticação). Respondem JSON com o status de cada dependência.

| Método | Rota | Descrição |
|---|---|---|
| GET | `/health/live` | Liveness — só verifica se o processo está no ar; não toca em dependências. Use para decidir **reiniciar** o container |
| GET | `/health/ready` | Readiness — verifica Oracle e Ollama. Use para decidir se o container recebe **tráfego** |
| GET | `/health` | Diagnóstico completo — todos os checks registrados |

Checks registrados: `api` (tag `live`), `oracle-db` (tags `ready,db,oracle`) e `ollama` (tags `ready,external`).

Códigos de status: `Healthy` e `Degraded` retornam **200**; `Unhealthy` retorna **503**. Falha no Oracle é `Unhealthy` (a API não atende sem banco); falha no Ollama é `Degraded` (só os recursos de IA param, o resto segue funcionando). O detalhamento do erro só aparece fora do ambiente de Produção.

```bash
curl http://localhost:5099/health/ready
```

### Auth
| Método | Rota | Descrição |
|---|---|---|
| POST | `/api/auth/registro/tutor` | Cadastro do Responsável pelo app — devolve a sessão com `consentimentoPendente` (RN-060) |
| POST | `/api/auth/login` | Autentica por e-mail e senha |
| POST | `/api/auth/refresh` | Renova o acesso rotacionando o refresh token |
| POST | `/api/auth/logout` | Encerra a sessão (idempotente) |
| GET | `/api/auth/me` | Perfil do usuário autenticado e pendências |
| POST | `/api/auth/trocar-senha` | Troca a senha e encerra as demais sessões |
| POST | `/api/auth/token` | **Obsoleto** — emite JWT sem senha; responde 404 fora de `Development` |

### Veterinarios
| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/veterinarios` | Lista todos ativos |
| GET | `/api/veterinarios/{id}` | Detalhe |
| GET | `/api/veterinarios/regiao/{uf}` | Por UF (ex: SP, RJ) |
| GET | `/api/veterinarios/{id}/agenda-config` | Configuração de agenda vigente (RN-034) |
| PUT | `/api/veterinarios/{id}/agenda-config` | Configura dias/horário/duração e materializa 60 dias de horários (RN-034) |
| GET | `/api/veterinarios/{id}/disponibilidade` | Horários livres por dia (RN-034/RN-035) |
| GET | `/api/veterinarios/me/extrato` | Extrato dos próprios atendimentos — alcançável pelo vet desativado (RN-024) |
| GET | `/api/veterinarios/{id}/servicos` | Serviços com valor e duração (RN-032) |
| PUT | `/api/veterinarios/{id}/servicos` | Define a vitrine de serviços (RN-032/RN-074) |
| GET | `/api/veterinarios/{id}/agenda` | Agenda futura de consultas |
| POST | `/api/veterinarios` | Cadastrar — requer role Admin (RN-107); aceita endereço (RN-026); devolve a **senha temporária** de primeiro acesso, uma única vez (P-05) |
| PUT | `/api/veterinarios/{id}` | Atualizar |
| GET | `/api/veterinarios/{id}/crmv` | Situação do CRMV junto ao conselho e reflexo no matching (RN-107) |
| POST | `/api/veterinarios/{id}/crmv` | Reconsulta o conselho e reaplica o resultado — requer role Admin (RN-107) |
| DELETE | `/api/veterinarios/{id}` | Desativar — requer role Admin, retorna agendamentos futuros (RN-022/RN-025) |

### Animais
| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/animais` | Lista todos ativos |
| GET | `/api/animais/{id}` | Detalhe |
| GET | `/api/animais/{id}/prontuarios` | Histórico longitudinal de prontuários |
| GET | `/api/animais/{id}/exames` | Exames do animal |
| POST | `/api/animais` | Cadastrar — exige `pesoKg`; aceita sexo, castrado, alergias, condições pré-existentes e carteira de vacinação |
| PUT | `/api/animais/{id}` | Atualizar — mesmos campos do cadastro |
| DELETE | `/api/animais/{id}` | Desativar (soft delete) |

### Tutores
| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/tutores` | Lista todos ativos — requer role Admin (RN-069/RN-106) |
| GET | `/api/tutores/{id}` | Detalhe |
| GET | `/api/tutores/{id}/animais` | Animais do tutor |
| POST | `/api/tutores` | Cadastrar |
| PUT | `/api/tutores/{id}` | Atualizar |
| GET | `/api/tutores/{id}/consentimentos` | Estado das 5 finalidades, com datas de concessão e revogação (RN-061) |
| PUT | `/api/tutores/{id}/consentimentos` | Concede ou revoga finalidades — não revoga por omissão (RN-061/RN-062) |
| GET | `/api/tutores/{id}/dispositivos` | Dispositivos ativos para push (RN-007/RN-092) |
| POST | `/api/tutores/{id}/dispositivos` | Registra dispositivo — idempotente por push token |
| DELETE | `/api/tutores/{id}/dispositivos/{dispositivoId}` | Remove dispositivo (remoção lógica) |
| DELETE | `/api/tutores/{id}` | Desativar (soft delete + anonimização LGPD) |

### Consultas
| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/consultas` | Lista paginada (`?pagina=&tamanho=`) com filtros: `dataInicio`, `dataFim`, `veterinarioId`, `tutorId`, `animalId`, `status`, `cancelada` |
| GET | `/api/consultas/{id}` | Detalhe |
| GET | `/api/consultas/veterinario/{id}` | Por veterinário |
| GET | `/api/consultas/animal/{id}` | Por animal |
| GET | `/api/consultas/{id}/briefing` | Pré-consulta: animal, histórico (últimas 5) e exames recentes (últimos 3) |
| POST | `/api/consultas/checkout` | Trava o horário por 10 min e cria a consulta em `EmCheckout` (RN-003/RN-035) |
| POST | `/api/consultas` | Agendar no ato — emergência presencial e balcão (RN-040) |
| POST | `/api/consultas/{id}/iniciar` | Abre a janela de captura — a consulta começa aqui (RN-008) |
| POST | `/api/consultas/{id}/captura/segmentos` | Recebe um trecho de áudio e enfileira a transcrição — 202 (RN-009) |
| GET | `/api/consultas/{id}/captura` | Situação da captura, com o texto já transcrito (RN-009) |
| POST | `/api/consultas/{id}/encerrar` | Fecha a janela e marca a consulta como `Realizada` (RN-008/RN-038) |
| GET | `/api/consultas/{id}/rascunho` | Prontuário estruturado pela IA — rascunho até o vet decidir (RN-080/RN-082) |
| PUT | `/api/consultas/{id}/validar-diagnostico` | Decisão sobre o rascunho: `Aprovado`, `Corrigido` ou `NaoAprovado` (RN-082) |
| POST | `/api/consultas/{id}/prontuario-manual` | Prontuário escrito à mão, sem IA no caminho (RN-085) |
| GET | `/api/consultas/{id}/auditoria-ia` | Trilha append-only das decisões sobre conteúdo de IA (RN-082) |
| POST | `/api/consultas/{id}/finalizar` | Finalizar — exige receita assinada (RN-087) |
| DELETE | `/api/consultas/{id}` | Cancelar + Strategy de reembolso (RN-014/RN-041/RN-042) |

**A janela de captura é explícita.** `iniciar` abre, `encerrar` fecha, e fora dela a IA não captura áudio nem produz conteúdo clínico (RN-079) — trecho enviado com a janela fechada devolve 409. O áudio vai em segmentos curtos, cada um com sua sequência: assim a transcrição acontece durante o atendimento e a falha de um trecho não derruba a consulta inteira. Reenvio da mesma sequência devolve 409, porque duplicaria o texto.

O despacho ao motor sai da requisição pelo worker: o veterinário não espera a transcrição para continuar atendendo. O motor devolve o texto em `POST /api/internos/stt/callback`, e **o contrato desse callback é da Vetly, não do motor** — trocar de fornecedor é mexer dentro do fluxo Node-RED, sem refazer o caminho de volta. Em desenvolvimento, `Adaptadores:Stt = "Simulado"` percorre o mesmo caminho assíncrono com texto sintético marcado como tal.

Encerrada a janela e transcritos os trechos, a IA estrutura o texto em prontuário (RN-080) — também fora da requisição, porque é lenta e o veterinário já saiu do atendimento. O rascunho guarda **a transcrição que o originou**: sem ela não há como conferir depois se a IA produziu algo que não foi dito, e sugestão que chega ao prontuário precisa ser auditável. Transcrição parcial gera rascunho parcial, com aviso e com a instrução explícita ao modelo de não preencher lacunas — perder a consulta inteira porque um trecho falhou seria pior. IA fora do ar ou rascunho sem conteúdo clínico caem no caminho manual em vez de travar a consulta: o atendimento aconteceu e precisa virar prontuário de algum jeito (RN-085).

A estruturação usa o `IOllamaService` que já servia diagnóstico, protocolo e triagem — `EstruturarConsultaAsync`, não um adaptador paralelo: é o mesmo motor e o mesmo contrato de sugestão.

**A decisão sobre o rascunho é explícita e tem três caminhos** (RN-082): `Aprovado` aceita o texto como veio; `Corrigido` exige o conteúdo corrigido, porque corrigir sem dizer o que mudou não é corrigir; `NaoAprovado` exige justificativa, encerra o ciclo sem documentos e **não** valida o diagnóstico — e sem validação não se gera documento. Não há aprovação por omissão, e um rascunho só é decidido uma vez: uma segunda decisão deixaria a trilha ambígua.

Toda decisão vira registro em `TB_LOG_AUDITORIA_IA`, que é **append-only**: o contrato do repositório só tem adicionar e ler, e a entidade não expõe mutação depois de gravada. Cada registro guarda o conteúdo final inteiro como o veterinário aceitou — não um diff, porque reconstruir o que foi assinado a partir de diferenças é frágil justamente quando mais importa — junto de quem decidiu, do modelo que sugeriu e de se houve alteração. É o que sustenta a afirmação de que nenhuma sugestão chegou ao prontuário sem decisão humana.

Recusado o rascunho, ou nunca tendo havido captura, `POST /api/consultas/{id}/prontuario-manual` fecha o atendimento à mão (RN-085) — conteúdo escrito pelo próprio veterinário já é conteúdo validado. Com rascunho ainda pendente devolve 409: seriam dois prontuários concorrentes sobre o mesmo atendimento.

No plano Básico a consulta inicia normalmente, **sem captura** (RN-085): o prontuário é preenchido à mão. `iniciar` também devolve os avisos que o veterinário precisa ver antes de começar — `PesoAusente` é o mais importante, porque sem peso não há sugestão de dose (RN-081) e descobrir isso no fim do atendimento seria tarde.

### Internações
| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/internacoes` | Lista todas |
| GET | `/api/internacoes/{id}` | Detalhe |
| POST | `/api/internacoes` | Abrir internação |
| PUT | `/api/internacoes/{id}/procedimentos` | Registrar procedimentos do dia e acumular valor apurado (RN-100) |
| POST | `/api/internacoes/{id}/alta` | Dar alta — retorna saldo restante (caução − total apurado) |

### Exames
| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/exames` | Lista todos |
| GET | `/api/exames/{id}` | Detalhe |
| POST | `/api/exames` | Solicitar exame |
| PUT | `/api/exames/{id}/resultado` | Registrar resultado (`{"resultado":"..."}`) |
| PUT | `/api/exames/{id}/liberar` | Liberar resultado ao tutor |

### Documentos
| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/documentos/consulta/{id}` | Documentos de uma consulta |
| GET | `/api/documentos/{id}` | Detalhe |
| GET | `/api/documentos/animal/{id}` | Board do pet: documentos publicados de um animal (RN-011/RN-090) |
| POST | `/api/documentos/consulta/{id}?tipo={TipoDocumento}&subtipo={TipoAtestado}` | Gerar via Factory, com conteúdo e PDF — exige diagnóstico validado (RN-082/RN-083) |
| POST | `/api/documentos/{id}/assinar` | Assinar pelo adaptador de assinatura — só o vet do atendimento (RN-087) |
| POST | `/api/documentos/{id}/publicar` | Publicar no board do pet (RN-011/RN-090) |
| POST | `/api/documentos/{id}/lido` | Registrar que o Responsável abriu o documento |
| POST | `/api/documentos/{id}/correcao` | Criar versão corrigida — após 24h exige justificativa (RN-088/RN-089) |

**Gerar documento é formatar, não inferir.** O conteúdo sai do estado final aprovado pelo veterinário, lido da trilha de auditoria (RN-083) — não do rascunho da IA. Sem conteúdo aprovado a geração devolve 422: decida sobre o rascunho ou registre o prontuário manual antes. Se a factory consultasse a IA de novo, o que fosse impresso poderia divergir do que o profissional aprovou.

Cada tipo formata o que lhe cabe: o prontuário registra o atendimento na ordem clínica; a receita sai da conduta e recusa emissão sem prescrição, porque receita vazia pareceria válida; o atestado muda o **texto** conforme o subtipo, não só o rótulo (RN-086); a nota fiscal é recibo e diz em letras claras que não substitui documento fiscal. Seção sem conteúdo é omitida — impressa em branco, pareceria documento incompleto.

O PDF é anexado na mesma chamada e entra pelo registro de mídia comum, então sua URL é sempre temporária (RN-090). O gerador é próprio, sem biblioteca de PDF: escreve um PDF 1.4 em Helvetica, uma das 14 fontes que todo leitor já traz. Para o que o MVP precisa — um documento legível que o Responsável leva para outra clínica — trazer uma dependência seria adicionar infraestrutura sem necessidade (§11); quando o documento ganhar identidade visual e QR de verificação, troca-se a implementação de `IGeradorDePdf`.

**Assinatura só onde ela significa alguma coisa (C-04).** A RN-087 exigia receita assinada para finalizar *qualquer* consulta — mas rotina, vacinação e retorno frequentemente não prescrevem nada, e a regra assim levaria o veterinário a emitir receita vazia só para conseguir fechar o atendimento, que é o oposto do que ela protege. O que passou a valer: **todo documento já emitido que exige assinatura precisa estar assinado**. Receita e atestado exigem, porque saem da plataforma afirmando algo em nome de um profissional habilitado; prontuário é o registro interno e a nota fiscal é recibo, e nenhum dos dois faz essa afirmação para fora. Consulta sem esses documentos finaliza normalmente.

A assinatura passa por `IAssinaturaAdapter`, escolhido por `Adaptadores:Assinatura`. No MVP é o nome digitado, conferido contra o nome registrado (tolerando caixa, acento e espaço repetido — recusar por um acento faltando seria rigor no lugar errado). **O carimbo entra no corpo do documento e diz como ele foi assinado**, inclusive que não habilita dispensação de controlado fora da plataforma: omitir isso deixaria o documento parecer mais do que é. Só o veterinário que conduziu o atendimento assina, e documento já assinado devolve 409.

**Gerar e publicar são passos separados**: o veterinário gera, confere e só então entrega. Receita sem assinatura não é publicada — no board ela pareceria válida sem ser (RN-087). Publicar é idempotente: republicar preserva a data original, que é a referência da notificação ao Responsável.

### Pagamentos
| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/pagamentos` | Lista paginada (`?pagina=&tamanho=`) |
| GET | `/api/pagamentos/{id}` | Detalhe |
| POST | `/api/pagamentos` | Cria a cobrança com o split apurado — responde 202, pagamento fica **pendente** (RN-006/RN-070) |
| GET | `/api/pagamentos/{id}/status` | Polling do checkout: status da cobrança e da consulta |
| POST | `/api/pagamentos/{id}/processar-split` | Split financeiro via Strategy (autônomo 80% / vinculado 60%) |

### Empresas
| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/empresas` | Lista todas ativas |
| GET | `/api/empresas/{id}` | Detalhe |
| GET | `/api/empresas/{id}/veterinarios` | Veterinários vinculados |
| POST | `/api/empresas` | Cadastrar |
| POST | `/api/empresas/{id}/veterinarios/{vetId}` | Vincular veterinário |
| PUT | `/api/empresas/{id}` | Atualizar |
| DELETE | `/api/empresas/{id}` | Desativar |

### Lembretes
| Método | Rota | Descrição |
|---|---|---|
| POST | `/api/lembretes` | Agendar lembrete (vacina, retorno, medicação…) |
| POST | `/api/lembretes/{id}/tentativa` | Registrar tentativa de contato — após 3 sem resposta, alerta à clínica (RN-095) |
| POST | `/api/lembretes/{id}/resposta` | Registrar resposta do tutor — encerra régua (RN-094) |

### Mídia e storage

| Método | Rota | Descrição |
|---|---|---|
| POST | `/api/midia/upload-url` | Reserva espaço no storage e devolve URL temporária de upload (§2.6) |
| GET | `/api/midia/{id}/url` | URL temporária de leitura — conteúdo clínico nunca vira URL pública (RN-090) |

A API **nunca proxia os bytes**: registra a mídia e o app fala direto com o storage. O `midiaId` é o que viaja nos payloads de negócio, nunca a URL, que expira em 15 minutos. Em desenvolvimento o storage é uma pasta em disco, com URLs assinadas por HMAC; em produção, um bucket S3-compatível.

Áudio de consulta é a única mídia com prazo: 30 dias para reprocessamento e depois some (P-06). Conteúdo clínico não expira, por guarda regulatória.

### Painel do veterinário

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/dashboard/veterinario` | Agenda do dia, pendências e números do mês (`?data=`) (RN-105) |

**Não é relatório: é o que precisa da atenção dele agora.** A ordem das seções segue a ordem em que as coisas travam — pendência de documentação bloqueia pagamento, agenda define o dia, números do mês são contexto.

`TemPendencia` conta só o que trava dinheiro ou documento: consulta iniciada e nunca encerrada, rascunho sem decisão, documento que exige assinatura e não a tem. Avaliação sem resposta aparece no painel mas **não** acende o aviso — responder é desejável, não bloqueante. A agenda marca o animal sem peso cadastrado, porque descobrir isso durante a consulta é tarde (RN-081), e omite consulta cancelada: o painel serve para conduzir o dia.

Não há id de veterinário na rota — o escopo vem do token, e nem o Admin pede o painel de outro por aqui.

### Notificações

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/notificacoes/tutor/{id}` | Caixa de entrada do Responsável (`?apenasNaoLidas=`) (RN-092) |
| POST | `/api/notificacoes/{id}/lida` | Registra que o Responsável abriu no app |

**A notificação é gravada antes de ser enviada.** O app precisa de uma caixa de entrada que sobrevive ao push perdido — aparelho desligado, token trocado, permissão negada — e o histórico do que foi comunicado é o que permite responder "avisamos?" depois. `NaoEntregue` não é o fim da linha: a notificação segue visível na caixa, porque push perdido não pode significar aviso perdido.

O envio sai da requisição, numa rotina de um minuto: o Responsável não pode esperar o APNs responder para que a consulta seja confirmada. Token que o provedor recusa como inválido **desativa o dispositivo** — app desinstalado e token rotacionado são o caso comum, não a exceção; falha do provedor, ao contrário, não desativa nada. O push passa por `IPushAdapter`, escolhido por `Adaptadores:Push`.

**A régua de lembretes** (rotina diária) transforma obrigação vencendo em aviso: sem ela, o board de obrigações é uma tela que só quem abre o app descobre — e quem já esqueceu da vacina é exatamente quem não abre. É **um aviso por animal, não por obrigação**, e nomeia a mais urgente em vez de dizer "você tem pendências", porque aviso genérico não move ninguém. Há intervalo mínimo de 7 dias entre dois avisos do mesmo animal: avisar de hora em hora sobre a mesma vacina transformaria cuidado em incômodo, e o Responsável desligaria a notificação inteira. Cada aviso cria também o `LembreteAgendado` que sustenta a régua — três tentativas sem resposta acionam o alerta à clínica (RN-095).

### Avaliações

| Método | Rota | Descrição |
|---|---|---|
| POST | `/api/avaliacoes/consulta/{id}` | Avalia um atendimento realizado (RN-055) |
| GET | `/api/avaliacoes/veterinario/{id}` | Reputação, com distribuição das notas (RN-057) |
| POST | `/api/avaliacoes/{id}/resposta` | Resposta pública do veterinário — uma só |
| POST | `/api/avaliacoes/{id}/moderar` | Esconde o comentário; a nota continua contando |

**Só avalia quem foi atendido, e só uma vez por consulta.** É o que separa reputação de campanha: sem o vínculo com um atendimento realizado, a nota vira número que qualquer um pode empurrar. O prazo é de 30 dias — avaliação muito posterior mede memória, não atendimento — e a nota não é editável depois de enviada, porque corrigir avaliação abriria a porta para pressão sobre quem avaliou. O índice único em `CONSULTA_ID` é a invariante: sem ele, duas requisições simultâneas passariam pela verificação e gravariam as duas.

A **moderação esconde o comentário e preserva a nota**. O contrário transformaria a moderação em ferramenta para apagar crítica, e há um teste que fixa isso. Moderar exige motivo: moderação sem motivo não se audita.

A reputação em `TB_VETERINARIO` é **recalculada** a partir das avaliações, não incrementada — média acumulada em campo diverge do que está gravado assim que uma avaliação é moderada ou corrigida. Abaixo de 3 avaliações a nota não é pública nem entra no score (RN-057): uma nota 5 vinda de uma única avaliação não diz nada sobre o profissional, e o matching usa o selo "Novo na Vetly" nesse intervalo (RN-033).

### Fidelidade

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/fidelidade/tutor/{id}/saldo` | Saldo de pontos e o que vence em 30 dias (RN-052) |
| GET | `/api/fidelidade/tutor/{id}/extrato` | Extrato append-only dos lançamentos (RN-051/RN-052) |

O resgate entra no checkout: `POST /api/pagamentos` aceita `pontosAResgatar`.

**O saldo é a soma dos lançamentos, não um campo guardado.** Saldo à parte diverge do extrato no primeiro erro, e aí não há como saber qual dos dois está certo. `TB_MOVIMENTO_PONTOS` é append-only e a coluna `PONTOS` é assinada — negativa em débito e expiração — justamente para que a soma feche. Corrigir um crédito indevido é lançar o débito correspondente, como em contabilidade.

Consulta realizada e **paga** rende 1 ponto por real (RN-052); 100 pontos valem R$ 1,00 no resgate. Consulta cancelada ou com pagamento não confirmado não credita — o programa pagaria por receita que não entrou. O crédito vale um ano, e o saldo mostra o que vence nos próximos 30 dias: avisar antes é o que separa um programa de fidelidade de uma pegadinha.

**Quem paga o desconto é a plataforma (RN-051).** Esta era a pendência da onda 4, e a decisão foi a mais conservadora: o desconto do resgate **sai da comissão da Vetly, não do repasse ao prestador**. O valor bruto continua sendo o preço do serviço, e é sobre ele que o repasse é calculado; o que muda é só o que a plataforma retém. Fazer o veterinário custear um programa de fidelidade que ele não ofereceu seria tirar dinheiro de terceiro. Por consequência, o resgate é limitado à comissão daquela cobrança — a Vetly banca a própria fidelidade, mas não paga para atender — e a mensagem de erro diz quantos pontos cabem.

A expiração é uma rotina diária, e a baixa entra como **lançamento** no extrato em vez de o saldo cair sozinho. Ponto já gasto não expira de novo: quem resgatou e depois viu o crédito vencer não fica devendo pontos que usou legitimamente.

### Obrigações do pet

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/obrigacoes/animal/{id}` | Board de obrigações — vencidas primeiro (RN-045) |
| POST | `/api/obrigacoes/animal/{id}` | Cria obrigação recorrente de cuidado (RN-045) |
| POST | `/api/obrigacoes/{id}/cumprir` | Registra o cumprimento e empurra o próximo vencimento |
| DELETE | `/api/obrigacoes/{id}` | Arquiva — sai do board, fica no histórico |
| POST | `/api/obrigacoes/animal/{id}/derivar-da-carteira` | Cria obrigações a partir da carteira de vacinação (RN-046) |

**A obrigação guarda a periodicidade, não uma data solta.** Cumprir empurra o próximo vencimento sozinho — sem isso, cada cumprimento exigiria alguém lembrar de reagendar o seguinte, que é exatamente o que falha. E o próximo vencimento conta a partir do **cumprimento**, não do vencimento anterior: quem vacinou com dois meses de atraso não deve receber o próximo aviso dois meses adiantado.

`Vencendo` é uma situação separada de `EmDia`, com janela de 30 dias, porque avisar só no vencimento é avisar tarde: agendar consulta leva dias. O board ordena por urgência e traz a contagem por situação junto da lista — a primeira pergunta do Responsável não é "quais são", é "tem alguma coisa atrasada?".

Obrigação de uma vez só (`periodicidadeEmDias = 0`, um retorno pontual) se arquiva ao ser cumprida, em vez de ficar eternamente vencida no board. A derivação da carteira é idempotente e conta a partir da dose mais recente de cada tipo — doses antigas do mesmo tipo são histórico, não obrigações separadas.

### Colmeia — histórico entre clínicas

| Método | Rota | Descrição |
|---|---|---|
| POST | `/api/colmeia` | Responsável autoriza um veterinário a alcançar o histórico do animal (RN-090) |
| DELETE | `/api/colmeia/{id}` | Revoga a autorização (RN-062/RN-090) |
| GET | `/api/colmeia/animal/{id}` | Autorizações de um animal — quem alcança o quê |
| GET | `/api/colmeia/animal/{id}/acessos` | Quem leu o histórico, o quê e quando |

**Quem concede é o Responsável, não a clínica.** O histórico do animal é dele, e o veterinário de fora só alcança o que foi autorizado, pelo tempo autorizado. A clínica que quisesse se autoconceder acesso é exatamente o que a guarda impede. Sem isso, "compartilhar o histórico" viraria "qualquer profissional cadastrado lê o prontuário de qualquer animal".

A concessão **nasce com prazo** — 30 dias por padrão, 365 no máximo: acesso clínico que não expira sozinho é acesso que ninguém lembra de revogar. E tem escopo, porque compartilhar quase nunca quer dizer tudo: `HistoricoCompleto`, `UltimaConsulta` ou `Documentos` — pedir segunda opinião sobre um exame não é abrir o prontuário desde filhote.

Todo acesso vai para `TB_LOG_ACESSO_COLMEIA`, **append-only**, inclusive a tentativa negada — que é justamente o que se quer enxergar numa auditoria. Autorização sem registro seria um cheque em branco; registro sem autorização não seria acesso, seria vazamento. Revogar encerra a autorização e **não** apaga o log: é isso que o Responsável precisa poder conferir depois.

### Rotas internas (serviço-a-serviço)

| Método | Rota | Descrição |
|---|---|---|
| POST | `/api/internos/pagamentos/webhook` | Evento de status do pagamento — **estado autoritativo** da transação |
| POST | `/api/internos/stt/callback` | Texto devolvido pelo motor de transcrição (RN-009) |

Autenticadas por `X-Vetly-Service-Token`, não por JWT de usuário: quem chama é um provedor, não uma pessoa. Sem token configurado a rota fica indisponível.

O webhook é o que confirma o pagamento: `Confirmado` promove a consulta de `EmCheckout` para `Confirmada` e ocupa o horário; `Recusado` expira a consulta e libera o horário. Reentrega de evento já processado responde 200 sem efeito — webhook é entregue mais de uma vez por natureza.

### Lista de espera

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/lista-espera` | Pedidos do próprio Responsável |
| POST | `/api/lista-espera` | Entra na fila de um veterinário (RN-004) |
| DELETE | `/api/lista-espera/{id}` | Sai da fila |
| POST | `/api/lista-espera/{id}/confirmar` | Aceita a vaga e segue para o checkout (RN-037) |

### Busca e matching

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/busca` | Clínicas e vets autônomos por proximidade e necessidade, ordenados pelo score (RN-001 a RN-033) |

Parâmetros: `animalId` (obrigatório), `necessidade`, `lat`/`lng` **ou** `cep`, `raioKm` (1–25, padrão 10), `especialidade`, `valorMinimo`, `valorMaximo`, `atendeHoje`, `pagina`, `tamanho`.

O score combina **distância 40%, avaliação 30% e disponibilidade 30%** (RN-030). Prestador sem as 3 avaliações mínimas (RN-057) é ordenado só por distância e disponibilidade, com os pesos renormalizados em 57/43 — sem boost artificial e sem nota inventada (RN-033, P-09). Cada item traz a composição do score, para o app poder explicar a ordem.

### IA (Ollama)
| Método | Rota | Descrição |
|---|---|---|
| POST | `/api/ia/diagnostico` | Sugerir hipóteses diagnósticas |
| POST | `/api/ia/protocolo` | Sugerir protocolo de tratamento |
| POST | `/api/ia/triagem` | Triar sintomas por urgência |
| POST | `/api/ia/orientacoes` | Orientações pós-atendimento para o tutor |

> **Todas as respostas da IA são sugestões — o veterinário deve validar manualmente antes de gerar qualquer documento clínico (RN-082).**

---

### Paginação

As listagens grandes (`GET /api/consultas`, `GET /api/pagamentos`) aceitam `?pagina=` e `?tamanho=` e respondem com o envelope:

```json
{ "itens": [ ... ], "total": 45, "pagina": 1, "tamanho": 20, "totalDePaginas": 3, "temProximaPagina": true }
```

Sem parâmetros valem página 1 e 20 itens. O tamanho é limitado a 100 por página — valores fora da faixa são normalizados, não rejeitados.

## Regras de Negócio

> A numeração segue o documento técnico oficial (`vetly-tech.md`, RN-001 a RN-107). As versões anteriores deste
> README usavam uma numeração própria que colidia com códigos diferentes do documento técnico — o de-para foi
> aplicado ao código, às exceções e a esta tabela.

| Código | Descrição | Implementação |
|---|---|---|
| RN-006 | Consulta só pode ser agendada se o pagamento estiver com status Confirmado | `ConsultaService.AgendarAsync` |
| RN-022/RN-025 | Desativação de veterinário encerra o acesso e retorna agendamentos futuros ao chamador | `VeterinarioService.DesativarAsync` |
| RN-004 | Sem horário disponível, o Responsável entra na lista de espera do veterinário | `ListaEsperaService` |
| RN-037 | Vaga liberada é oferecida ao primeiro da fila com prioridade de 15 min; vencida, passa ao próximo | `ItemListaEspera` + `PromoverProximoAsync` |
| RN-026 | Endereço persistido no próprio registro, com latitude/longitude **derivadas dele** pela geocodificação — o payload do cliente é ignorado | `Endereco` + `IGeocodificacaoAdapter` |
| RN-033/RN-057 | Nota só é pública a partir de 3 avaliações; `PUBLICADO_EM` ancora o selo "Novo na Vetly" por 30 dias | `Veterinario.TemNotaPublica` + `PublicarNoMatching` |
| RN-105 | O painel é sempre do próprio veterinário e destaca só o que trava dinheiro ou documento; avaliação sem resposta não conta como pendência bloqueante | `DashboardService.ObterDoVeterinarioAsync` |
| RN-092 | Notificação é gravada antes de enviada e sobrevive ao push perdido; token recusado como inválido desativa o dispositivo, falha de provedor não | `Notificacao` + `NotificacaoService` + `IPushAdapter` |
| RN-094/RN-095 | Régua diária transforma obrigação vencendo em um aviso por animal, com intervalo mínimo de 7 dias, e cria o lembrete que aciona a clínica após 3 tentativas | `AvisarObrigacoesVencendo` + `LembreteAgendado` |
| RN-055 | Só o Responsável atendido avalia, uma vez por consulta e em até 30 dias; índice único garante a invariante sob concorrência | `Avaliacao` + `AvaliacaoService` |
| RN-057 | Reputação recalculada a partir das avaliações; abaixo de 3 a nota não é pública nem entra no score, e comentário moderado não tira a nota da média | `AvaliacaoService.RecalcularReputacaoAsync` + `Veterinario.TemNotaPublica` |
| RN-051 | O desconto do resgate sai da comissão da plataforma, não do repasse: o bruto e o repasse não mudam, e o resgate é limitado à comissão daquela cobrança | `Pagamento.AplicarDesconto` + `PagamentoService.AplicarResgateAsync` |
| RN-052 | Consulta realizada e paga rende 1 ponto por real; o saldo é a soma de um extrato append-only, e o crédito expira em um ano com lançamento de baixa | `MovimentoDePontos` + `FidelidadeService` |
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

## Modelo Entidade-Relacionamento

    TB_TUTOR {
        CHAR(36) ID PK
        VARCHAR2(200) NOME
        VARCHAR2(254) EMAIL
        VARCHAR2(20) TELEFONE
        NUMBER(1) CONSENTIMENTO_ATENDIMENTO
        NUMBER(1) CONSENTIMENTO_LEMBRETES
        NUMBER(1) ATIVO
    }

    TB_ANIMAL {
        CHAR(36) ID PK
        VARCHAR2(200) NOME
        VARCHAR2(100) ESPECIE
        VARCHAR2(100) RACA
        DATE DATA_NASCIMENTO
        CHAR(36) TUTOR_ID FK
        NUMBER(1) ATIVO
    }

    TB_VETERINARIO {
        CHAR(36) ID PK
        VARCHAR2(200) NOME
        VARCHAR2(15) CRMV
        CHAR(2) UF_ATUACAO
        NUMBER PERSONA
        NUMBER PLANO
        CHAR(36) EMPRESA_ID FK
        NUMBER(1) ATIVO
    }

    TB_EMPRESA {
        CHAR(36) ID PK
        VARCHAR2(300) NOME
        VARCHAR2(100) TIPO
        CHAR(36) ADMINISTRADOR_ID FK
        NUMBER(1) ATIVA
    }

    TB_CONSULTA {
        CHAR(36) ID PK
        TIMESTAMP DATA_HORA
        NUMBER MODALIDADE
        CHAR(36) VETERINARIO_ID FK
        CHAR(36) ANIMAL_ID FK
        CHAR(36) TUTOR_ID FK
        NUMBER STATUS_PAGAMENTO
        NUMBER(1) DIAGNOSTICO_VALIDADO
        NUMBER(1) CANCELADA
        NUMBER(1) FINALIZADA
    }

    TB_PAGAMENTO {
        CHAR(36) ID PK
        CHAR(36) TUTOR_ID FK
        CHAR(36) CONSULTA_ID FK
        CHAR(36) INTERNACAO_ID FK
        NUMBER(18_2) VALOR
        NUMBER MEIO_PAGAMENTO
        NUMBER STATUS_PAGAMENTO
        NUMBER(18_2) PERCENTUAL_SPLIT
    }

    TB_INTERNACAO {
        CHAR(36) ID PK
        CHAR(36) ANIMAL_ID FK
        CHAR(36) VETERINARIO_ID FK
        NUMBER(18_2) VALOR_CAUCAO
        NUMBER(18_2) VALOR_TOTAL_APURADO
        TIMESTAMP DATA_ABERTURA
        TIMESTAMP DATA_ALTA
        CLOB PROCEDIMENTOS_DIARIOS
    }

    TB_EXAME {
        CHAR(36) ID PK
        CHAR(36) ANIMAL_ID FK
        CHAR(36) VETERINARIO_ID FK
        VARCHAR2(500) TIPO_SOLICITACAO
        CLOB RESULTADO
        NUMBER(1) LIBERADO_AO_TUTOR
        TIMESTAMP DATA_SOLICITACAO
    }

    TB_DOCUMENTO {
        CHAR(36) ID PK
        CHAR(36) CONSULTA_ID FK
        CHAR(36) INTERNACAO_ID FK
        NUMBER TIPO_DOCUMENTO
        NUMBER VERSAO
        NUMBER(1) ASSINADO_DIGITALMENTE
        CHAR(36) VERSAO_ORIGINAL_ID FK
        TIMESTAMP DATA_GERACAO
    }

    TB_PRONTUARIO {
        CHAR(36) ID PK
        CHAR(36) CONSULTA_ID FK
        CHAR(36) VETERINARIO_ID FK
        CLOB DADOS_CLINICOS
        CHAR(36) VERSAO_ORIGINAL_ID FK
        TIMESTAMP DATA_REGISTRO
    }

    TB_LEMBRETE {
        CHAR(36) ID PK
        CHAR(36) ANIMAL_ID FK
        CHAR(36) TUTOR_ID FK
        NUMBER TIPO
        TIMESTAMP DATA_EVENTO
        NUMBER TENTATIVAS_REALIZADAS
        NUMBER(1) TUTOR_RESPONDEU
        NUMBER(1) ALERTA_ENVIADO_CLINICA
    }
