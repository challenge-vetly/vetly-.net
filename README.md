# Vetly API

**Plataforma intermediária entre Responsáveis de pets e veterinários, construída em ASP.NET Core 10 sobre Oracle, com IA local na consulta e observabilidade de produção.**

O Vetly não presta serviço veterinário: ele conecta, relaciona e retém Responsáveis e veterinários, organiza o agendamento, cobra, reparte e guarda o histórico. Esta API é a implementação dessa plataforma — o backend inteiro do produto, do onboarding do Responsável até a nota fiscal com split apurado, passando por agenda com controle de concorrência, captura de áudio da consulta, estruturação do prontuário por LLM, programa de fidelidade com contabilidade FIFO de pontos e uma trilha de auditoria append-only para tudo que a IA sugere.

A tese que organiza o produto é simples de enunciar e difícil de implementar: **o prontuário pertence ao animal, não à clínica**. Um Responsável que muda de bairro, troca de veterinário ou cai num plantão de emergência às três da manhã leva o histórico completo do animal junto — porque o histórico nunca esteve na clínica, esteve na plataforma. Isso desloca a diferenciação entre prestadores de "quem tem o prontuário" para "preço, qualidade, infraestrutura e equipamentos", que é um nivelamento estratégico desejado, e transforma a Vetly no ativo insubstituível da relação. Quase toda decisão de arquitetura deste repositório existe para sustentar essa frase: o modelo de consentimento granular, a colmeia por evento clínico com prazo e escopo, o log de acesso append-only visível ao Responsável, a permanência do histórico após o desligamento do profissional.

A API é uma implementação de **MVP**, e isso é explícito no código, não uma desculpa. Não há gateway de pagamento real: a cobrança é simulada, e nota fiscal e split são **registrados, não liquidados**. O que não é simulado é o resto — a apuração do split por plano está correta até o centavo, o webhook é a fonte autoritativa do estado da transação, a idempotência protege reenvios, e a concorrência no horário é resolvida por token de concorrência no banco, não por `lock` em memória. As dependências externas entram por **porta** na camada de Aplicação e saem por um adaptador `*Simulado` na Infrastructure, escolhido por configuração: trocar "simulado" por "real" é trocar o registro no contêiner de injeção de dependência, sem tocar em nenhum serviço.

Esta versão da documentação acrescenta a camada que faltava para que o sistema pudesse ser operado, e não apenas executado: **monitoramento e observabilidade** (health checks, log estruturado, tracing distribuído e métricas) e uma suíte de **testes automatizados** de 915 casos organizados por camada. As duas coisas respondem à mesma pergunta em momentos diferentes — a suíte responde "isto está certo?" antes do deploy; a observabilidade responde "isto continua certo?" depois dele.

---

## Sumário

| Seção | O que você encontra |
|---|---|
| [1. Arquitetura](#1-arquitetura) | As quatro camadas, a regra de dependência e por que ela é inegociável |
| [2. Stack e tecnologias](#2-stack-e-tecnologias) | Cada biblioteca do projeto e para que exatamente ela serve aqui |
| [3. Padrões de projeto](#3-padrões-de-projeto-aplicados) | Factory, Strategy, Repository, Adapter e onde cada um vive |
| [4. Monitoramento e observabilidade](#4-monitoramento-e-observabilidade) | Health checks, Serilog, OpenTelemetry, catálogo de métricas e o playbook de plantão |
| [5. Testes automatizados](#5-testes-automatizados) | Padrão AAA, nomenclatura, fixtures, execução e cobertura |
| [6. Instalação e execução](#6-instalação-e-execução) | Pré-requisitos, configuração local, migrations e como subir |
| [7. Fluxo de captura da consulta](#7-fluxo-de-captura-da-consulta) | Ponta a ponta, formato do áudio, troca de motor e as variáveis do Azure Speech |
| [8. Autenticação](#8-autenticação) | JWT, refresh rotativo, perfis e policies |
| [9. Roteiro de testes no Postman](#9-roteiro-de-testes-no-postman) | A jornada completa, chamada por chamada, na ordem em que funciona |
| [10. Referência de endpoints](#10-referência-de-endpoints) | Todas as rotas, agrupadas por área, com o que cada uma faz |
| [11. Correções de segurança](#11-correções-de-segurança) | O que a revisão de segurança encontrou e como foi fechado |
| [12. Modelo entidade-relacionamento](#12-modelo-entidade-relacionamento) | As tabelas principais e seus campos |
| [Regras de negócio](REGRAS-DE-NEGOCIO.md) | Documento separado: RN-001 a RN-107 e onde cada uma é implementada |

---

## 1. Arquitetura

O projeto é dividido em quatro camadas, com a regra de dependência apontando **sempre para dentro**:

```
┌──────────────────────────────────────────────────────────────────────────┐
│  Vetly.API                                                               │
│  Controllers · Filters · Middlewares · HealthChecks · Observability       │
│  Jobs (worker hospedado) · Security (identidade da requisição)            │
│  ↓ referencia Application e Infrastructure                                │
├──────────────────────────────────────────────────────────────────────────┤
│  Vetly.Infrastructure                                                     │
│  EF Core + Oracle · Repositories · Migrations · Adapters (*Simulado)       │
│  Jobs (handlers e rotinas) · Security (JWT, PBKDF2)                        │
│  ↓ referencia Application e Domain                                        │
├──────────────────────────────────────────────────────────────────────────┤
│  Vetly.Application                                                        │
│  Services (as regras) · Interfaces (portas) · DTOs · Factories            │
│  Strategies · Exceptions · Observability (spans e métricas de negócio)     │
│  ↓ referencia apenas Domain                                               │
├──────────────────────────────────────────────────────────────────────────┤
│  Vetly.Domain                                                             │
│  Entities · Value Objects · Enums — sem dependência de pacote nenhum      │
└──────────────────────────────────────────────────────────────────────────┘
```

**O Domain não referencia nada.** Nem EF Core, nem ASP.NET Core, nem sequer as abstrações de logging da Microsoft. Uma entidade que precisasse de um pacote para existir não seria mais um modelo de negócio, seria um modelo de persistência com outro nome. O mapeamento para o Oracle acontece inteiramente na Infrastructure, em classes `IEntityTypeConfiguration<T>` — o `Animal` não sabe que existe uma tabela `TB_ANIMAL`, e é isso que permite que as invariantes dele sejam testadas sem banco nenhum, em milissegundos.

**A Application depende só de abstrações.** As duas únicas dependências de pacote são `Microsoft.Extensions.Configuration.Abstractions` e `Microsoft.Extensions.Logging.Abstractions` — contratos, não implementações. Tudo que ela precisa do mundo externo (banco, storage, gateway de pagamento, conselho regional de medicina veterinária, motor de transcrição, push) é declarado como **interface** aqui e implementado lá fora. É o Princípio de Inversão de Dependência aplicado literalmente: `ConsultaService` conhece `IConsultaRepository`, e nunca `ConsultaRepository`.

A instrumentação de observabilidade também vive na Application, em `Observability/VetlyTelemetry.cs`, pelo mesmo raciocínio das abstrações de log: `ActivitySource` e `Meter` são tipos da própria BCL, e usá-los não acopla a camada a OpenTelemetry, a Prometheus nem a fornecedor nenhum. Quem escolhe o exportador é o `Program.cs`. A alternativa — instrumentar só na borda HTTP — produziria métricas de transporte ("quantos 200 saíram") em vez de métricas de negócio ("quantos checkouts viraram consulta confirmada"), e são as segundas que dizem se o produto funciona.

**A API é uma casca fina.** Os controllers validam entrada, chamam um serviço e traduzem o resultado em HTTP. Nenhuma regra de negócio mora ali. O que a API acrescenta é o que só faz sentido na borda: autenticação, os três filtros globais que falham fechado (consentimento LGPD, bloqueio de veterinário desativado, idempotência), o middleware que converte exceção de domínio em `ProblemDetails`, os health checks e a telemetria HTTP.

---

## 2. Stack e tecnologias

Cada linha diz **para que a tecnologia é usada neste projeto**, não o que ela é em abstrato.

### Plataforma e persistência

| Tecnologia | Versão | Para que serve aqui |
|---|---|---|
| **.NET / ASP.NET Core** | 10.0 | Runtime e framework web. Hospeda a API, o worker de negócio no mesmo processo e o pipeline de middlewares. Minimal hosting no `Program.cs`, com controllers clássicos para as rotas |
| **Entity Framework Core** | 10.0.7 | ORM. Mapeamento por `IEntityTypeConfiguration`, migrations versionadas e — o ponto que mais importa aqui — **tokens de concorrência** nas colunas `ESTADO` e `LOCK_CONSULTA_ID` do horário, que é o que impede dois animais no mesmo slot |
| **Oracle.EntityFrameworkCore** | 10.23 | Provider Oracle do EF Core. O banco é Oracle 21c+; particularidades como "string vazia é `NULL`" moldaram decisões reais do modelo (daí o sentinela `";"` em pré-sintomas vazios) |
| **Oracle Database** | 21c+ | Banco relacional de produção. Guarda também a fila de jobs (`TB_JOB`) e os registros de idempotência — o worker não precisou de broker novo |

### Segurança e identidade

| Tecnologia | Versão | Para que serve aqui |
|---|---|---|
| **Microsoft.AspNetCore.Authentication.JwtBearer** | 10.0.7 | Valida o token em toda requisição autenticada: emissor, audiência, tempo de vida e assinatura |
| **System.IdentityModel.Tokens.Jwt** | 8.14 | Emissão do token no `GeradorDeTokenJwt` (Infrastructure). Access token de 8 horas; refresh de 30 dias, **rotativo** — reapresentar um refresh já usado derruba todas as sessões daquele usuário, porque é o sinal de que ele vazou |
| **PBKDF2-HMAC-SHA256** | BCL | Hash de senha com 210.000 iterações e salt por senha, no formato autodescritivo `pbkdf2$sha256$iteracoes$salt$hash`. Subir o custo no futuro não invalida as senhas já cadastradas |

### Observabilidade

| Tecnologia | Versão | Para que serve aqui |
|---|---|---|
| **Serilog.AspNetCore** | 10.0.0 | Provedor de log estruturado. Substitui o logging padrão, lê níveis e sinks do `appsettings.json` e escreve uma linha por requisição com método, rota, status e duração como **campos**, não como texto |
| **Serilog.Sinks.Console / File** | 6.1.1 / 7.0.0 | Dois destinos: console legível em desenvolvimento e arquivo JSON compacto (`logs/vetly-YYYYMMDD.log`) com rotação diária, teto de 20 MB e retenção de 14 dias |
| **Serilog.Enrichers.Environment / Thread** | 3.0.1 / 4.0.0 | Carimbam `MachineName` e `ThreadId` em toda linha — o que responde "o erro só acontece numa das instâncias?" sem precisar instrumentar nada |
| **OpenTelemetry.Extensions.Hosting** | 1.18.0 | Integra o SDK do OpenTelemetry ao host: um `Resource` compartilhado (nome, versão e ambiente do serviço) para traces e métricas |
| **OpenTelemetry.Instrumentation.AspNetCore** | 1.18.0 | Um span por requisição HTTP recebida, com exceção anexada. Sondas de saúde são filtradas para não afogarem o trace |
| **OpenTelemetry.Instrumentation.Http** | 1.18.0 | Spans das chamadas de **saída** — é assim que a latência do Ollama e do motor de transcrição aparece separada da nossa |
| **OpenTelemetry.Instrumentation.EntityFrameworkCore** | 1.18.0-beta.1 | Um span por consulta ao Oracle. Responde "a rota está lenta por causa do banco ou da regra?" sem precisar adivinhar |
| **OpenTelemetry.Instrumentation.Runtime** | 1.18.0 | Métricas de GC, heap, thread pool e exceções do runtime — o primeiro lugar a olhar quando a latência sobe sem que o banco tenha piorado |
| **OpenTelemetry.Exporter.Prometheus.AspNetCore** | 1.18.0-beta.1 | Publica o endpoint `/metrics` no formato de texto do Prometheus, pronto para raspagem por Prometheus, Grafana Agent ou Datadog |
| **OpenTelemetry.Exporter.OpenTelemetryProtocol** | 1.18.0 | Exportador OTLP para traces e métricas. Desligado por padrão e ativado por configuração — sem coletor do outro lado, ele só encheria o log de falhas de conexão |
| **OpenTelemetry.Exporter.Console** | 1.18.0 | Exportador de console para depurar a própria instrumentação em desenvolvimento, sob a chave `OpenTelemetry:ExportarParaConsole` |
| **Diagnostics.HealthChecks** | framework | Os três endpoints de saúde (`/health/live`, `/health/ready`, `/health`), com checks separados por tag e um escritor de resposta JSON próprio. Vem no framework compartilhado do ASP.NET Core |
| **HealthChecks.EntityFrameworkCore** | 10.0.7 | Referência direta. O check do Oracle abre conexão de verdade — não apenas valida a string de conexão |

### Documentação e testes

| Tecnologia | Versão | Para que serve aqui |
|---|---|---|
| **Microsoft.AspNetCore.OpenApi** | 10.0.7 | Gera o documento OpenAPI a partir dos controllers e dos DTOs |
| **Scalar.AspNetCore** | 2.14.11 | Interface interativa da documentação em `/scalar/v1` — navegação, exemplos e execução de chamadas direto do navegador |
| **xUnit** | 2.9.3 | Framework de testes das duas suítes. É o que fornece `IClassFixture` e `ICollectionFixture`, usados para compartilhar contexto entre testes |
| **Moq** | 4.20.72 | Dublês das interfaces da Application nos testes de unidade — repositórios, adaptadores e o escopo do usuário autenticado |
| **Microsoft.AspNetCore.Mvc.Testing** | 10.0.7 | `WebApplicationFactory`: sobe a API inteira em memória para os testes de integração, com pipeline, filtros e autenticação reais |
| **Microsoft.EntityFrameworkCore.InMemory** | 10.0.7 | Substitui o Oracle nos testes de integração. É a **única** peça trocada — todo o resto do sistema roda de verdade |
| **coverlet.collector** | 6.0.4 | Cobertura de código em formato Cobertura, filtrada por `coverlet.runsettings` para não contar migrations geradas |

### IA e serviços auxiliares

| Tecnologia | Versão | Para que serve aqui |
|---|---|---|
| **Ollama** | `llama3.1` | LLM local. Estrutura a transcrição da consulta em prontuário (RN-080), sugere hipóteses diagnósticas, protocolo com posologia, triagem de sintomas e orientações pós-atendimento. **Toda saída é sugestão** e passa por decisão explícita do veterinário (RN-082) |
| **Azure Speech-to-Text** | opcional | Motor de transcrição de fala em produção, chamado diretamente ([§7](#7-fluxo-de-captura-da-consulta)). O contrato do callback é da Vetly, não do motor — é o que torna a troca de fornecedor uma troca de registro, e não uma refatoração de fluxo. O fluxo **Node-RED** segue disponível como implementação alternativa da mesma porta |

---

## 3. Padrões de projeto aplicados

| Padrão | Onde vive | Que problema resolve |
|---|---|---|
| **Factory** | `DocumentoService` seleciona um `IDocumentoFactory` pelo `TipoDocumento` | Prontuário, receita, atestado e nota fiscal têm formatação e regras próprias. Um `switch` gigante viraria o lugar onde todo tipo novo é esquecido; com factories injetadas por `IEnumerable<T>`, adicionar um tipo é adicionar uma classe |
| **Strategy** | `ConsultaService` seleciona um `ICancelamentoStrategy` por antecedência (RN-041/RN-042) | As três faixas de reembolso (integral, parcial, nenhum) são políticas, não ramos de código. A simulação de cancelamento **reusa a mesma seleção** — mostrar um valor e cobrar outro é exatamente o que a regra proíbe |
| **Strategy** | `PagamentoService` seleciona um `ISplitFinanceiroStrategy` pelo plano | Take rate decrescente: Básico 15%, Profissional 12%, Enterprise 10%. A maior comissão pertence ao menor plano |
| **Repository** | Interfaces na Application, implementações EF Core na Infrastructure | Mantém o EF Core fora das regras. Também é onde a colisão de concorrência otimista é traduzida em 409, para que a camada de aplicação siga sem conhecer o ORM |
| **Adapter / Port** | `ICrmvAdapter`, `IPagamentoAdapter`, `IStorageAdapter`, `ISttAdapter`, `IPushAdapter`, `IAssinaturaAdapter`, `IGeocodificacaoAdapter` | Toda dependência externa entra por uma porta e sai por um adaptador escolhido em `Adaptadores:*`. No MVP são `*Simulado`; trocar por real é trocar o registro no DI |
| **Middleware** | `CorrelationIdMiddleware` → log de requisição → `MetricasHttpMiddleware` → `ExceptionHandlingMiddleware` | Preocupações transversais que nenhum controller deveria repetir: correlação, log, métrica e tradução de exceção em `ProblemDetails` (RFC 7807) |
| **Action Filter** | `ConsentimentoAtendimentoFilter`, `VetDesativadoFilter`, `IdempotencyFilter` | Guardas que **falham fechado**: valem em toda rota, e a exceção precisa se declarar explicitamente. Uma guarda que vale por opt-in é uma guarda que alguém vai esquecer |
| **Background worker** | `VetlyBackgroundService` + `TB_JOB` | Trabalho que não pode acontecer dentro da requisição: transcrição, estruturação por IA, expiração de locks, régua de lembretes, crédito de pontos |
| **Value Object** | `Crmv`, `Endereco`, `Geo`, `RegistroVacinacao`, `ConsentimentoRegistrado` | Conceitos que se validam sozinhos e são imutáveis. Um `Crmv` inválido não chega a existir |
| **Soft delete** | `Veterinario`, `Animal`, `Tutor` | Desativação em vez de exclusão. Prontuário que some quando alguém deleta um cadastro é o oposto do que a plataforma promete |
| **Inversão de dependência** | Toda a Application | Nenhum serviço conhece uma implementação concreta. É o que permite testar 713 casos de regra sem banco, sem HTTP e sem I/O |
| **ProblemDetails (RFC 7807)** | `ExceptionHandlingMiddleware` | Um formato de erro para toda a API, incluindo **503** para dependência externa fora do ar — 422 diria ao app que a culpa é dele |
| **Enums como string no JSON** | `JsonStringEnumConverter`, entrada e saída | O contrato trafega `"Presencial"`, não `1`. O contrato numérico é ilegível para o front e quebra a cada reordenação do enum; a persistência não muda |
| **Idempotência** | `IdempotencyFilter` + `TB_IDEMPOTENCIA` | Rotas marcadas exigem `Idempotency-Key` e reaproveitam a resposta por 24 h. É o que impede que um reenvio do app cobre duas vezes |
| **Concorrência otimista** | Tokens em `Slot.ESTADO` e `LOCK_CONSULTA_ID` | Dois processos lendo o mesmo horário livre no mesmo milissegundo. O banco decide quem fica com ele; o perdedor recebe 409 |

---

## 4. Monitoramento e observabilidade

Uma API sem observabilidade não é uma API mais simples: é uma API cujos problemas são descobertos pelo usuário. A diferença entre as duas aparece no dia em que alguém escreve "não consegui pagar às 14h32" — e o time descobre que a única forma de investigar é varrer log por horário torcendo para não ter havido duas tentativas no mesmo minuto.

A camada implementada aqui cobre os **três pilares**, e eles não se substituem porque respondem a perguntas diferentes:

| Pilar | Pergunta que responde | Custo | Ferramenta |
|---|---|---|---|
| **Métricas** | *Está ruim?* | Baixíssimo — números agregados | OpenTelemetry + Prometheus |
| **Traces** | *Ruim onde?* | Médio — amostrado em produção | OpenTelemetry + OTLP |
| **Logs** | *Ruim por quê?* | Alto — uma linha por evento | Serilog |

A métrica dispara o alerta, o trace aponta a camada culpada, o log traz o detalhe do caso concreto. Ter só um dos três significa, na prática, descobrir o incidente pelo cliente, ou saber que ele existe sem conseguir explicá-lo.

**O que costura os três é o `TraceId`.** Ele é gerado (ou aceito do cliente) no primeiro middleware do pipeline, gravado em `HttpContext.TraceIdentifier`, empilhado no `LogContext` do Serilog — de onde carimba toda linha de log de toda camada, sem que nenhum serviço precise recebê-lo por parâmetro —, anexado como tag do span e devolvido em dois lugares que o usuário alcança: o cabeçalho `X-Correlation-Id` e o campo `correlationId` do corpo de erro. Um chamado de suporte que começa com um id termina no span exato.

### 4.1 Health checks

Três endpoints públicos, sem autenticação, com semânticas deliberadamente diferentes:

| Endpoint | Executa | Decisão que sustenta |
|---|---|---|
| `GET /health/live` | Só o check `api` — não toca dependência nenhuma | **Reiniciar** o container |
| `GET /health/ready` | Checks com a tag `ready`: Oracle, Ollama e — quando `Adaptadores:Stt = Azure` — o Azure Speech | Enviar ou não **tráfego** para a instância |
| `GET /health` | Todos os checks registrados | Diagnóstico manual |

A separação entre *liveness* e *readiness* não é cerimônia. Liveness decide reiniciar o processo: se ela consultasse o banco, uma indisponibilidade momentânea do Oracle mataria containers perfeitamente saudáveis, e o restart em massa pioraria exatamente o incidente em curso. Readiness precisa do oposto — tocar as dependências e tirar a instância de rotação enquanto elas não respondem.

**A severidade de cada falha também é uma decisão de produto:**

| Check | Tags | Falha reportada como | Por quê |
|---|---|---|---|
| `api` | `live` | — | Se o processo responde à lambda, o host está vivo |
| `oracle-db` | `ready`, `db`, `oracle` | `Unhealthy` → **HTTP 503** | Sem banco a API não entrega nada. Tem de sair de rotação |
| `ollama` | `ready`, `external` | `Degraded` → **HTTP 200** | Sem IA só os recursos de IA param; agendar, pagar e emitir documento seguem funcionando. Derrubar a instância inteira por causa do LLM seria transformar uma degradação em indisponibilidade |
| `azure-speech` | `ready`, `external` | `Degraded` → **HTTP 200** | Registrado apenas com `Adaptadores:Stt = Azure`. Mesma razão do Ollama: sem o motor a captura de áudio para, mas a consulta continua acontecendo e o prontuário segue pelo caminho manual (RN-085) |

O check do Oracle usa uma consulta de teste customizada que **abre a conexão explicitamente**, em vez do `CanConnectAsync` padrão. O motivo é prático: o padrão engole a exceção e devolve apenas `false`, e o relatório sai sem motivo nenhum. Abrindo a conexão, o erro do Oracle (`ORA-01017`, por exemplo) sobe, é capturado pelo `HealthCheckService` e aparece no JSON — o que transforma "o banco está fora" em "a senha do usuário expirou".

O check do Azure Speech sonda o `issueToken` da região, e não um reconhecimento de verdade: um POST de áudio custaria quota a cada probe, e o que se quer saber — a região responde e a chave é aceita — o `issueToken` responde igual, de graça. Credencial recusada (401/403) sai com essa palavra na descrição, para o plantão não procurar rede onde o problema é chave.

O check do Ollama tem **timeout próprio de 5 segundos**, e não os 120 do `HttpClient` que o serviço usa: 120 segundos são adequados para inferência e absurdos para uma sonda. Um health check que trava é pior que um health check que falha.

A resposta é um JSON próprio, e não o texto `Healthy` que o ASP.NET Core devolve por padrão — porque `Unhealthy` sem dizer **qual** dependência caiu não ajuda ninguém às três da manhã:

```json
{
  "status": "Degraded",
  "duracaoTotalMs": 84.21,
  "checks": [
    { "nome": "oracle-db", "status": "Healthy",  "descricao": null, "duracaoMs": 12.4, "tags": ["ready","db","oracle"], "erro": null },
    { "nome": "ollama",    "status": "Degraded", "descricao": "Ollama nao respondeu em 5s.", "duracaoMs": 5002.1, "tags": ["ready","external"], "erro": null }
  ]
}
```

O campo `erro` **só é preenchido fora de Produção**. Mensagens de erro do Oracle expõem host, porta e código de erro, e isso não pode vazar por um endpoint público.

```bash
curl http://localhost:5140/health/live      # o processo está de pé?
curl http://localhost:5140/health/ready     # as dependências estão prontas?
curl http://localhost:5140/health           # relatório completo
```

### 4.2 Logging estruturado (Serilog)

O log da Vetly é **estruturado**: cada linha é um objeto com campos nomeados, não uma frase concatenada. A diferença é a diferença entre conseguir perguntar *"todas as requisições acima de 2 segundos, na rota de checkout, do usuário X"* e ter um `.txt` com frases.

**Configuração por ambiente, não por código.** Níveis, sinks e overrides por namespace vêm da seção `Serilog` do `appsettings.json` — mudar o nível de log de um namespace em produção não pode exigir recompilar e redeployar. O que fica em código são os enriquecedores, que são decisão de arquitetura:

| Enriquecedor | Campo adicionado | Para que serve |
|---|---|---|
| `FromLogContext` | `CorrelationId`, `TraceId` | **O mais importante.** Sem ele, as propriedades empilhadas pelo middleware de correlação não aparecem, e a correlação inteira deixa de existir |
| `WithProperty` | `Aplicacao`, `Versao`, `Ambiente` | Separa a Vetly dos demais serviços num log agregado, e o `Versao` permite comparar "antes" e "depois" de um deploy |
| `WithMachineName` | `MachineName` | Responde "o erro só acontece numa das instâncias?" |
| `WithThreadId` | `ThreadId` | Correlaciona linhas de um mesmo fluxo assíncrono quando o `TraceId` não basta |

**Dois destinos, com propósitos distintos.** O console usa um template legível por humanos, com o `CorrelationId` na frente para que se possa acompanhar o desenvolvimento a olho nu. O arquivo usa `CompactJsonFormatter` — uma linha JSON por evento, que é o formato que Seq, Elastic, Loki ou Datadog ingerem sem transformação. Rotação diária, teto de 20 MB por arquivo e retenção de 14 dias.

**Níveis com critério.** O padrão é `Information`, com `Microsoft` e `Microsoft.AspNetCore` rebaixados para `Warning` — o framework é verboso e o ruído dele afogaria o log da aplicação. `Microsoft.EntityFrameworkCore.Database.Command` também está em `Warning`: logar cada SQL executado em `Information` significaria despejar parâmetros de consulta no log, e parâmetros carregam nome de Responsável, id de animal e conteúdo clínico — dado sensível sob a LGPD.

Uma linha por requisição sai do `UseSerilogRequestLogging`, com regra de nível própria:

- exceção vazando até ali → **Error**, qualquer que seja a rota;
- status ≥ 500 → **Error**;
- status ≥ 400 → **Warning** — o cliente errou, não o servidor; acionar alerta de disponibilidade por um payload inválido é o caminho mais rápido para o time ignorar alertas;
- sonda de saúde → **Verbose**, porque um `/health/live` a cada 10 segundos são 8.640 linhas por dia e por instância. Elas não somem: baixar o nível na configuração as traz de volta quando se quer depurar o próprio probe;
- o resto → **Information**.

Exemplo real do arquivo JSON, com tudo que uma investigação precisa em uma única linha:

```json
{
  "@t": "2026-09-01T12:58:23.3779493Z",
  "@mt": "{RequestMethod} {RequestPath} respondeu {StatusCode} em {Elapsed:0.0000} ms",
  "@l": "Warning",
  "@tr": "3c041ea3875bc38c14a0c83a0dbd1362",
  "@sp": "bab6fa8cc853f137",
  "RequestMethod": "POST",
  "RequestPath": "/api/auth/login",
  "StatusCode": 422,
  "Elapsed": 192.6375,
  "CorrelationId": "3c041ea3875bc38c14a0c83a0dbd1362",
  "TraceId": "3c041ea3875bc38c14a0c83a0dbd1362",
  "Host": "localhost:5140",
  "Protocolo": "HTTP/1.1",
  "Aplicacao": "vetly-api",
  "Versao": "1.0.0",
  "Ambiente": "Development",
  "MachineName": "DESKTOP-RCHE303",
  "ThreadId": 23
}
```

`@tr` e `@sp` são o trace e o span do OpenTelemetry, incluídos automaticamente pelo Serilog quando há uma `Activity` corrente. É por eles que se salta do log para o trace no backend.

**O identificador de correlação segue uma ordem de precedência deliberada:**

1. o cabeçalho `X-Correlation-Id` enviado pelo cliente, se vier — é assim que o app mobile amarra a jornada dele à nossa;
2. o `TraceId` do W3C Trace Context da `Activity` corrente — o mesmo id que o OpenTelemetry exporta, o que faz log e trace se encontrarem sem nenhuma conversão;
3. o `TraceIdentifier` do Kestrel, que sempre existe.

O valor vindo do cliente é truncado em 128 caracteres e tem quebras de linha neutralizadas: cabeçalho é entrada do usuário, e sem limite alguém inflaria cada linha de log com o que quisesse — ou injetaria uma quebra de linha para quebrar o parsing.

### 4.3 Tracing distribuído (OpenTelemetry)

O tracing responde onde o tempo foi gasto **dentro** da requisição. A instrumentação automática cobre as três fronteiras que uma requisição da Vetly atravessa:

| Instrumentação | O que produz |
|---|---|
| ASP.NET Core | O span raiz da requisição, com rota, método, status e exceção anexada |
| Entity Framework Core | Um span filho por consulta ao Oracle — é o que separa "a regra está lenta" de "o banco está lento" |
| HttpClient | Um span filho por chamada de saída: Ollama e o motor de transcrição aparecem com a latência deles isolada da nossa |

Isso ainda não é suficiente. Os spans automáticos param na fronteira do controller e não sabem que dentro daquele `POST` houve uma trava de horário, uma leitura de cadastro e uma escrita transacional. Por isso a camada de Aplicação abre **spans de domínio** próprios, pela `ActivitySource` `Vetly.Application`:

| Span | Onde é aberto | O que revela |
|---|---|---|
| `consulta.checkout` | `ConsultaService.IniciarCheckoutAsync` | Quanto do checkout foi trava de horário e quanto foi leitura de cadastro |
| `pagamento.webhook` | `PagamentoService.ProcessarWebhookAsync` | O caminho mais crítico do sistema: é o webhook, não a resposta síncrona, que confirma a consulta (RN-006) |
| `documento.gerar` | `DocumentoService.GerarAsync` | Qual das três etapas custou — trilha de auditoria, montagem do conteúdo ou renderização do PDF |
| `ia.<operacao>` | `OllamaService.EnviarAsync` | `ActivityKind.Client`, com o modelo em tag. É o que faz o LLM aparecer como dependência externa |
| `worker.ciclo` | `VetlyBackgroundService.ExecutarCicloAsync` | O worker roda fora de qualquer requisição HTTP; sem um span raiz próprio, todo o trabalho dele seria invisível |

**Exceções de negócio marcam o span como erro.** Do ponto de vista do transporte, um 422 é uma resposta bem-sucedida — o span sairia verde. Quem investiga "por que o agendamento não completou" precisa que o span diga que a operação terminou em RN-035. O `ExceptionHandlingMiddleware` faz isso para toda exceção que passa por ele.

**Exportação.** O OTLP é o protocolo padrão e fala com Jaeger, Tempo, Grafana Cloud, Honeycomb, Datadog e qualquer coletor compatível. Fica **desligado por padrão**, porque ligado sem coletor do outro lado ele tentaria exportar em lote a cada poucos segundos e encheria o log de falhas de conexão — barulho que faz a observabilidade parecer o problema. Para ligar, basta configurar o endpoint:

```json
{
  "OpenTelemetry": {
    "ServiceName": "vetly-api",
    "ExportarParaConsole": false,
    "Otlp": { "Endpoint": "http://localhost:4317" }
  }
}
```

Um Jaeger local para ver os traces sobe com um comando:

```bash
docker run -d --name jaeger -p 16686:16686 -p 4317:4317 jaegertracing/all-in-one:latest
# UI em http://localhost:16686 — procure pelo serviço "vetly-api"
```

Para depurar a própria instrumentação sem subir nada, `"ExportarParaConsole": true` despeja spans e métricas no console.

**Privacidade no trace.** Os valores dos parâmetros de consulta SQL não são exportados. O texto do SQL é inofensivo — vem parametrizado —, mas os valores carregam nome de Responsável, id de animal e conteúdo clínico, que são dado sensível sob a LGPD (§7.2 do documento de produto) e não podem sair para um backend de tracing de terceiro.

### 4.4 Métricas

`GET /metrics` publica, no formato de texto do Prometheus, três famílias de métricas em uma única resposta.

**Da plataforma e do runtime** — vindas dos medidores `Microsoft.AspNetCore.Hosting`, `Microsoft.AspNetCore.Server.Kestrel`, `System.Net.Http` e da instrumentação de runtime: duração de requisição, conexões ativas do Kestrel, pool de conexões HTTP de saída, GC, heap, thread pool e contagem de exceções.

**Da borda HTTP da Vetly** — medidor `Vetly.Http`, com a rota template e a classe de status já resolvidas:

| Métrica | Tipo | Tags | Para que serve |
|---|---|---|---|
| `vetly.http.requisicoes` | Counter | `metodo`, `rota`, `classe`, `status` | Volume por rota |
| `vetly.http.erros` | Counter | `metodo`, `rota`, `classe`, `status` | Dividido pela anterior no mesmo recorte, é a **taxa de erros** |
| `vetly.http.duracao` | Histogram (ms) | `metodo`, `rota`, `classe` | **Tempo de resposta**, com percentis |

Histograma e não média: média de latência esconde exatamente o que interessa. Uma rota com média de 90 ms e p99 de 4 s tem um problema real que a média nunca mostra.

**De negócio** — medidor `Vetly.Negocio`, emitido pela camada de Aplicação. São as métricas que o produto precisa provar (§10 do documento de produto), e nenhuma delas poderia ser derivada de códigos HTTP:

| Métrica | Tipo | Tags | Pergunta que responde |
|---|---|---|---|
| `vetly.checkouts.iniciados` | Counter | `prestador` (clinica/autonomo) | Quantos checkouts abriram |
| `vetly.consultas.confirmadas` | Counter | — | Quantos viraram consulta paga. **A razão entre as duas é a taxa de conversão do agendamento** |
| `vetly.consultas.canceladas` | Counter | `faixa` (Strategy aplicada) | Cancelamento concentrado numa faixa é sinal de política mal calibrada |
| `vetly.pagamentos.processados` | Counter | `status` (confirmado/recusado/inalterado) | Taxa de recusa. `inalterado` é reentrega de webhook, contada à parte para não contaminar a série |
| `vetly.pagamentos.valor` | Histogram (BRL) | — | Distribuição do ticket — base do split e do take rate efetivo |
| `vetly.documentos.emitidos` | Counter | `tipo` | A consulta está saindo com prontuário, receita e NF, ou só com prontuário? |
| `vetly.ia.decisoes` | Counter | `decisao` (Aprovado/Corrigido/NaoAprovado) | **A métrica central do MVP**: aprovado ÷ total é a proporção de documentos gerados pela IA sem edição relevante |
| `vetly.ia.duracao` | Histogram (ms) | `operacao`, `resultado` | Separa "a API está lenta" de "o modelo está lento" |
| `vetly.regras.violadas` | Counter | `codigo` (RN-xxx) | Qual regra está sendo tocada, e com que frequência |
| `vetly.jobs.executados` | Counter | `tipo`, `resultado` | Fila que parou de drenar — antes de virar lembrete não entregue |
| `vetly.worker.ciclo.duracao` | Histogram (ms) | — | Ciclo maior que o intervalo de 30 s é o sinal antecedente de fila crescendo |
| `vetly.notificacoes.despachadas` | Counter | `canal`, `resultado` | Queda na entrega quase sempre é credencial de provedor vencida |

`vetly.regras.violadas` merece destaque porque é o contador mais útil do conjunto para operação. Uma RN que dispara o tempo todo raramente é usuário mal-intencionado: quase sempre é tela deixando o usuário tentar o que a regra proíbe. E um pico em RN-105 (escopo por linha) — esse sim — é incidente de segurança, não usabilidade.

**Cardinalidade é a regra que não se quebra.** A tag de rota usa o *template* (`api/consultas/{id}`), nunca o path concreto: o path concreto criaria uma série temporal nova por consulta agendada, e um Prometheus com milhões de séries é um Prometheus fora do ar. Pela mesma razão a tag de status inclui a *classe* (`2xx`, `4xx`, `5xx`), e requisições que não casam rota nenhuma são agrupadas sob `(sem rota)` — o que impede um scanner de URLs de criar uma série por tentativa. **Sondas de saúde e a própria raspagem de métricas ficam fora** das métricas de negócio: um `/health/live` a cada poucos segundos dominaria a contagem e faria a latência média parecer excelente, porque a maioria das "requisições" não faz nada.

Consultas úteis no Prometheus:

```promql
# Taxa de erro por rota, nos últimos 5 minutos
sum by (rota) (rate(vetly_http_erros_total[5m]))
  / sum by (rota) (rate(vetly_http_requisicoes_total[5m]))

# p95 de tempo de resposta por rota
histogram_quantile(0.95,
  sum by (le, rota) (rate(vetly_http_duracao_milliseconds_bucket[5m])))

# Conversão do funil de agendamento
sum(rate(vetly_consultas_confirmadas_total[1h]))
  / sum(rate(vetly_checkouts_iniciados_total[1h]))

# Regras de negócio mais violadas na última hora
topk(10, sum by (codigo) (rate(vetly_regras_violadas_total[1h])))

# Aprovação da IA sem correção (a métrica-alvo do MVP)
sum(rate(vetly_ia_decisoes_total{decisao="Aprovado"}[24h]))
  / sum(rate(vetly_ia_decisoes_total[24h]))

# p99 de latência do LLM
histogram_quantile(0.99, sum by (le, operacao) (rate(vetly_ia_duracao_milliseconds_bucket[15m])))
```

Um Prometheus local para raspar a API:

```yaml
# prometheus.yml
scrape_configs:
  - job_name: vetly-api
    scrape_interval: 15s
    static_configs:
      - targets: ['host.docker.internal:5140']
```

```bash
docker run -d --name prometheus -p 9090:9090 \
  -v "$PWD/prometheus.yml:/etc/prometheus/prometheus.yml" prom/prometheus
```

### 4.5 Como monitorar na prática

O roteiro abaixo é o que se faz de fato quando algo parece errado, na ordem em que se faz:

**1. A instância está viva?** `GET /health/live`. Se responde 200, o processo está de pé e o problema não é reiniciar o container.

**2. Ela consegue atender?** `GET /health/ready`. `503` aponta o Oracle; `200` com `Degraded` aponta o Ollama — e nesse caso só os recursos de IA estão afetados.

**3. Está ruim para todo mundo ou para uma rota?** No `/metrics`, `vetly_http_erros_total` e `vetly_http_duracao_milliseconds` agrupados por `rota` respondem em segundos. Erro concentrado numa rota é bug; espalhado por todas é infraestrutura.

**4. É erro de servidor ou regra sendo violada?** `vetly_regras_violadas_total` por `codigo`. Um pico em `RN-035` significa disputa por horário — talvez agenda mal configurada. Um pico em `RN-060` significa gente tentando usar o app sem consentimento — provavelmente a tela de onboarding quebrou. Nenhum dos dois é erro de servidor, e nenhum dos dois apareceria como 5xx.

**5. Onde o tempo está indo?** No backend de traces, filtre por `service.name = vetly-api` e ordene por duração. Os spans filhos separam banco, LLM e regra.

**6. O que exatamente aconteceu com aquele usuário?** Pegue o `correlationId` que o cliente recebeu no corpo do erro e procure por ele no log — todas as linhas daquela requisição, de todas as camadas, saem carimbadas com o mesmo valor.

```bash
# Toda a história de uma requisição, do arquivo JSON
grep '"CorrelationId":"3c041ea3875bc38c14a0c83a0dbd1362"' logs/vetly-*.log
```

---

## 5. Testes automatizados

A suíte tem **915 testes**, todos verdes, divididos em dois projetos por natureza do que verificam — e não por conveniência de organização:

| Projeto | Testes | O que verifica | Como |
|---|---|---|---|
| `tests/Vetly.UnitTests` | **713** | Domínio e Aplicação: invariantes de entidade e regras de serviço | xUnit + Moq. Sem banco, sem HTTP, sem I/O — a suíte inteira roda em menos de um segundo |
| `tests/Vetly.IntegrationTests` | **202** | A API de ponta a ponta, por HTTP | `WebApplicationFactory` com a aplicação real; só o Oracle é trocado por InMemory |

A divisão importa porque as duas suítes pegam classes diferentes de defeito. Os testes de unidade provam que **cada regra está certa isoladamente**: que cancelar com 25 horas de antecedência devolve reembolso integral, que 100 pontos valem R$ 3,00, que um CRMV fora do formato não vira entidade. Os testes de integração provam que **elas se encaixam** — e pegam o que nenhum teste de unidade pegaria: o campo que o serviço preenche e o controller não devolve, a rota que existe mas não aceita o payload que a anterior produziu, o filtro que barra o caminho legítimo, a policy que exige uma role que ninguém emite. Uma API pode ter todas as regras certas e ainda assim não ser atravessável.

### 5.1 Padrão AAA e nomenclatura

Todo teste segue **Arrange, Act, Assert**, nessa ordem, com uma única ação no Act:

```csharp
[Fact]
public async Task ChamadaAoLlm_QuandoOModeloFalha_RegistraDuracaoComResultadoDeFalha()
{
    // Arrange — o registro no finally é o que garante isto. Medir só no caminho
    // feliz esconderia exatamente o caso que interessa: o timeout, que é longo
    // por definição e sai por exceção.
    _coletor.Limpar();
    var servico = CriarServico(status: HttpStatusCode.InternalServerError);
    var contexto = new ContextoClinicoDto { /* ... */ };

    // Act
    await Assert.ThrowsAnyAsync<HttpRequestException>(
        () => servico.SugerirDiagnosticoAsync(contexto));

    // Assert
    var medicao = _coletor.De("vetly.ia.duracao")
        .LastOrDefault(m => m.Tag("operacao") == "SugerirDiagnosticoAsync");

    Assert.NotNull(medicao);
    Assert.Equal("falha", medicao.Tag("resultado"));
}
```

O Arrange às vezes vive no construtor da classe ou em um método auxiliar — que é o idioma do xUnit para setup compartilhado — mas a separação das três fases é sempre visível. Um teste com dois Acts não é um teste: é dois testes que falham juntos e não dizem qual regra quebrou.

A nomenclatura é **`MetodoTestado_Cenario_ResultadoEsperado`**, e a razão é que o nome de um teste é lido no relatório de falha do CI, onde ninguém tem o código na frente:

| Nome | O que se lê sem abrir o arquivo |
|---|---|
| `Credito_PorObrigacaoCumprida_DaCinquentaPontosFixos` | Creditar por obrigação cumprida dá 50 pontos fixos |
| `TravarParaCheckout_SlotJaTravado_RecusaOSegundoCheckout` | Travar um slot já travado recusa o segundo checkout |
| `HealthLive_NaoExecutaChecksDeDependencia` | Liveness não toca dependência |
| `RespostaDeErro_TrazNoProblemDetailsOMesmoIdDoCabecalho` | O `ProblemDetails` traz o mesmo id do cabeçalho |

### 5.2 Fixtures e Collection Fixtures

O xUnit oferece dois níveis de compartilhamento de contexto, e o projeto usa os dois — cada um onde faz sentido.

**`IClassFixture<T>`** compartilha uma instância entre os testes de **uma classe**. Serve para setup caro que não deve vazar entre classes.

**`ICollectionFixture<T>`** compartilha uma instância entre **todas as classes de uma coleção**. É o que o projeto usa nos dois casos em que o recurso é, por natureza, de processo:

| Coleção | Fixture | Por que precisa ser de coleção |
|---|---|---|
| `ColecaoDaApi` | `VetlyWebApplicationFactory` | Subir o host ASP.NET Core é caro. Com `IClassFixture`, as 11 classes de integração subiriam 11 hosts, cada um com contêiner de DI, pipeline e worker próprios. E como o nome do banco InMemory é estático, todas já compartilhavam o mesmo banco: o isolamento que múltiplos hosts aparentavam dar nunca existiu. A coleção torna essa realidade explícita em vez de deixá-la como armadilha |
| `ColecaoDeTelemetria` | `ColetorDeTelemetriaFixture` | Um `MeterListener` e um `ActivityListener` são inscrições de **processo**. Vários listeners simultâneos sobre os mesmos instrumentos estáticos funcionam, mas duplicam trabalho e tornam a ordem de callback imprevisível |

```csharp
// tests/Vetly.IntegrationTests/Fixtures/ColecaoDaApi.cs
[CollectionDefinition(Nome)]
public sealed class ColecaoDaApi : ICollectionFixture<VetlyWebApplicationFactory>
{
    public const string Nome = "API Vetly (host compartilhado)";
}

// e cada classe de teste declara:
[Collection(ColecaoDaApi.Nome)]
public class JornadaCompletaTests { /* ... */ }
```

**O contrato que isso impõe** vale registrar, porque é a fonte clássica de teste intermitente: o xUnit não paraleliza classes da mesma coleção, então elas rodam em sequência — mas compartilham estado no banco. Cada teste cria os próprios dados com identificadores únicos (e-mail com `Guid`, CRMV aleatório) em vez de depender de uma linha específica já existente. É a mesma disciplina que um banco de homologação compartilhado exige.

O `ColetorDeTelemetriaFixture` resolve um problema mais sutil: **instrumentação é o caso clássico de código que "não dá para testar"** e que, por isso, quebra sem ninguém notar. Alguém remove uma linha de `Add` num refactor e a métrica simplesmente deixa de existir — nenhum teste falha, nenhum comportamento muda, e o painel só some meses depois. O `MeterListener` e o `ActivityListener` são a API que a própria BCL oferece para fechar essa lacuna: são os mesmos mecanismos que o OpenTelemetry usa por baixo, apontados para uma lista em memória.

Como outras coleções rodam em paralelo e também exercitam os serviços instrumentados, as asserções são sempre do tipo *"contém uma medição com estas tags"*, nunca *"recebeu exatamente uma medição"*, e os testes usam valores-sentinela próprios, impossíveis de colidir com o restante da suíte.

### 5.3 Como executar

```bash
# Toda a suíte
dotnet test

# Só uma das camadas
dotnet test tests/Vetly.UnitTests
dotnet test tests/Vetly.IntegrationTests

# Um arquivo ou um teste específico
dotnet test --filter "FullyQualifiedName~FidelidadeTests"
dotnet test --filter "FullyQualifiedName~ObservabilidadeTests"
dotnet test --filter "Name~RegistraDuracaoComResultadoDeFalha"

# Saída detalhada, com o nome de cada teste
dotnet test --logger "console;verbosity=detailed"

# Sem recompilar (útil ao rodar em ciclo)
dotnet build && dotnet test --no-build
```

Os testes de integração **não precisam de Oracle nem de Ollama**: o banco é substituído por InMemory e os adaptadores externos são os `*Simulado` padrão. `dotnet test` funciona em uma máquina recém-clonada, sem configuração nenhuma.

### 5.4 Cobertura

```bash
# Coleta com os filtros do projeto (exclui migrations geradas)
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings

# Relatório HTML navegável
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage" -reporttypes:Html
```

Números da última execução completa:

| Suíte | Cobertura de linhas | Por projeto |
|---|---|---|
| Unitários | **86,1%** | Application 89,2% · Domain 82,9% |
| Integração | **69,9%** | API 91,1% · Infrastructure 96,7% · Application 48,0% · Domain 47,1% |

O `coverlet.runsettings` existe porque, sem ele, o número mente por omissão: as migrations do EF Core são cerca de 7 mil linhas de **código gerado**, que nenhum teste executa e nenhum teste deveria executar — elas afundam a cobertura da Infrastructure de 96% para 4% e transformam a métrica em ruído. A regra do filtro é simples: exclui-se o que não é decisão humana (código gerado) e o que não tem comportamento a verificar; não se exclui nada que contenha regra.

---

## 6. Instalação e execução

### Pré-requisitos

| Requisito | Necessário para | Observação |
|---|---|---|
| **.NET 10 SDK** | Compilar e executar | `dotnet --version` deve começar com `10.` |
| **Oracle Database 21c+** | Persistência | A connection string padrão aponta para `oracle.fiap.com.br:1521/orcl` |
| **Ollama + `llama3.1`** | Recursos de IA | Opcional para subir a API: sem ele, `/health/ready` reporta `Degraded` e só as rotas de IA param |

Instalação do Ollama em https://ollama.com/download e, depois:

```bash
ollama serve
ollama pull llama3.1

# validar
curl -X POST http://localhost:11434/api/generate \
  -H "Content-Type: application/json" \
  -d '{"model":"llama3.1","prompt":"ola","stream":false}'
```

### Configuração local

Crie `src/Vetly.API/appsettings.Development.local.json` — este arquivo está no `.gitignore` e **não é commitado**. Credencial, connection string e chave JWT não vão para o repositório:

```json
{
  "ConnectionStrings": {
    "OracleConnection": "User Id=SEU_USUARIO;Password=SUA_SENHA;Data Source=oracle.fiap.com.br:1521/orcl"
  },
  "Jwt": {
    "Key": "VetlySecretKey_MustBeAtLeast32CharactersLong!"
  },
  "Servicos": {
    "TokenInterno": "escolha-um-token-de-servico-longo-e-aleatorio"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama3.1",
    "TimeoutSeconds": 120
  },
  "OpenTelemetry": {
    "ExportarParaConsole": false,
    "Otlp": { "Endpoint": "" }
  }
}
```

O `Servicos:TokenInterno` autentica as rotas serviço-a-serviço (webhook de pagamento e callback de transcrição). Sem token configurado, essas rotas ficam indisponíveis — o que é o comportamento correto: uma rota que confirma pagamento não pode existir sem autenticação.

### Subir

```bash
# 1. Restaurar e compilar — o build deve terminar limpo com -warnaserror
dotnet restore
dotnet build -warnaserror

# 2. Aplicar as migrations no Oracle
dotnet ef database update --project src/Vetly.Infrastructure --startup-project src/Vetly.API

# 3. Executar
dotnet run --project src/Vetly.API --launch-profile https   # https://localhost:7262
dotnet run --project src/Vetly.API --launch-profile http    # http://localhost:5140

# 4. Rodar a suíte de testes
dotnet test
```

### Endereços úteis

| Endereço | O que é |
|---|---|
| `https://localhost:7262/scalar/v1` | Documentação interativa (Scalar) — navegar e executar chamadas |
| `https://localhost:7262/openapi/v1.json` | Documento OpenAPI bruto, para importar no Postman ou no Insomnia |
| `http://localhost:5140/health` | Relatório de saúde completo |
| `http://localhost:5140/metrics` | Métricas no formato Prometheus |
| `src/Vetly.API/logs/vetly-AAAAMMDD.log` | Log estruturado em JSON, com rotação diária |

### Chaves de configuração

| Chave | Padrão | O que controla |
|---|---|---|
| `Adaptadores:Crmv` | `Simulado` | Consulta ao conselho regional (RN-107) |
| `Adaptadores:Pagamento` | `Simulado` | Provedor de cobrança e webhook (RN-006) |
| `Adaptadores:Storage` | `Local` | Onde a mídia é guardada — disco em dev, bucket S3-compatível em produção |
| `Adaptadores:Stt` | `Simulado` | Motor de transcrição: `Simulado`, `Azure` ou `NodeRed` (RN-009) — ver [§7](#7-fluxo-de-captura-da-consulta) |
| `Adaptadores:Assinatura` | `NomeDigitado` | Assinatura de documentos (RN-087) |
| `Adaptadores:Push` | `Simulado` | Envio de push (RN-092) |
| `Adaptadores:Geocodificacao` | `Simulado` | Coordenadas a partir do endereço (RN-026) |
| `Storage:PublicBaseUrl` | `https://localhost:7262` | Origem da URL assinada. **Obrigatória**: sem ela a API não sobe — o motor de transcrição busca o áudio de fora do processo, e URL relativa não é endereço |
| `Azure:Speech:Region` | `canadacentral` | Região do recurso de Speech. A **chave** nunca vem daqui — só de `AZURE_SPEECH_KEY` |
| `Serilog:MinimumLevel:Default` | `Information` | Nível de log da aplicação |
| `OpenTelemetry:Otlp:Endpoint` | vazio | Endpoint OTLP. Vazio desliga a exportação |
| `OpenTelemetry:ExportarParaConsole` | `false` | Despeja spans e métricas no console, para depurar a instrumentação |

---

## 7. Fluxo de captura da consulta

O veterinário abre a janela com "iniciar consulta", o app grava o áudio em trechos
curtos, cada trecho vira texto e, fechada a janela, a transcrição inteira é
estruturada em prontuário pela IA (RN-008/RN-009/RN-079/RN-080).

### O caminho, ponta a ponta

| # | Chamada | O que acontece |
|---|---|---|
| 1 | `POST /api/consultas/{id}/iniciar` | Abre a janela e devolve `gravacao` — formato, duração do trecho e sample rate que o app deve usar |
| 2 | `POST /api/midia/upload-url` | Reserva espaço no storage para um trecho e devolve `midiaId` + `uploadUrl` assinada |
| 3 | `PUT {uploadUrl}` | O app envia os bytes **direto ao storage**; a API nunca proxia áudio |
| 4 | `POST /api/consultas/{id}/captura/segmentos` | Registra o trecho pelo `midiaId` e enfileira a transcrição — responde `202` |
| 5 | *(worker)* | Despacha ao motor com uma URL de leitura assinada e um token por segmento |
| 6 | *(motor)* | Devolve o texto pelo contrato da Vetly — por `POST /api/internos/stt/callback` ou por dentro, no caso do Azure |
| 7 | `GET /api/consultas/{id}/captura` | Progresso para a barra de status: recebidos, transcritos, com falha e o texto parcial |
| 8 | `POST /api/consultas/{id}/encerrar` | Fecha a janela, marca a consulta como realizada e decide o desfecho da sessão |
| 9 | `GET /api/consultas/{id}/rascunho` | O rascunho estruturado, quando houver — é aqui que o app faz polling |

A sessão **sempre chega a um estado terminal**: `GerandoRascunho` (todos os trechos
transcreveram), `TranscricaoParcial` (parte transcreveu — o rascunho sai com o que há,
com aviso) ou `SemTranscricao` (nenhum — o caminho é
`POST /api/consultas/{id}/prontuario-manual`, RN-085). Trecho despachado cujo callback
não volta em **3 minutos** é retentado, e esgotadas as tentativas vira
`Falha(Timeout)` — é o que impede a sessão de ficar presa esperando um motor que
morreu calado.

### Formato do áudio

O front grava com a `MediaRecorder` do navegador, em **`audio/ogg;codecs=opus`, 16 kHz
mono, trechos de 30 segundos**. Não é escolha estética: a REST API de reconhecimento
de fala curta do Azure aceita **apenas WAV (PCM) e OGG (OPUS)**. O `MediaRecorder`
grava OGG-OPUS nativamente, então o front não ganha dependência nenhuma por causa
disso.

**O WebM não dá erro — ele emudece.** Medido contra o serviço real: um WebM/Opus
válido volta com `HTTP 200` e `RecognitionStatus: "Success"`, mas com `DisplayText`
vazio e confiança `0.0`. O `Content-Type` declarado não muda nada, porque o Azure
inspeciona o container e não confia no cabeçalho. É por isso que o adaptador mantém uma
lista fechada de formatos e **recusa antes de chamar**: sem ela o trecho viraria
`AudioIlegivel` depois de gastar chamada e retentativas, e o veterinário leria "áudio
ilegível" quando o problema é o container.

Os parâmetros não precisam ser adivinhados: vêm no `gravacao` da resposta de
`/iniciar`. O front deve lê-los de lá, e não fixá-los no código.

```js
const { gravacao } = await iniciarConsulta(consultaId);

const gravador = new MediaRecorder(stream, { mimeType: gravacao.formato });
gravador.start(gravacao.segundosPorSegmento * 1000);   // um blob por trecho
```

### Trocar de motor

O motor é escolhido por `Adaptadores:Stt`, e trocar de fornecedor é trocar esse valor
— nenhum serviço muda, porque **o contrato do callback é da Vetly, não do motor**.

| Valor | Quando usar |
|---|---|
| `Simulado` | **Padrão.** Desenvolvimento e suíte de testes: devolve texto sintético, explicitamente marcado, pelo mesmo caminho assíncrono do motor real. Não consome quota |
| `Azure` | Azure Speech-to-Text, chamado diretamente. É o caminho recomendado em produção |
| `NodeRed` | Fluxo Node-RED. Continua sendo uma implementação válida da porta, mas não é mais o caminho recomendado |

### Variáveis de ambiente do Azure Speech

A chave **nunca** vai para `appsettings.json` — vem só do ambiente:

```bash
# Linux / macOS
export AZURE_SPEECH_KEY="<a chave do recurso de Speech>"
export AZURE_SPEECH_REGION="canadacentral"
```

```powershell
# Windows (sessão atual)
$env:AZURE_SPEECH_KEY = "<a chave do recurso de Speech>"
$env:AZURE_SPEECH_REGION = "canadacentral"
```

O repositório tem um `.env` **gitignorado** com essas duas variáveis, para quem prefere
carregá-las de arquivo:

```bash
set -a && . ./.env && set +a
dotnet run --project src/Vetly.API --launch-profile https
```

Com `Adaptadores:Stt = Azure`, a API **não sobe** sem `AZURE_SPEECH_KEY`. É deliberado:
subir sem a chave só adiaria a descoberta para o primeiro segmento que não transcreve.
O endpoint é montado a partir da região — `https://{região}.stt.speech.microsoft.com/…`
—, e não configurado inteiro, porque endereço digitado à mão é a forma mais fácil de
apontar para a região errada.

`/health/ready` passa a incluir o check `azure-speech`, que sonda o `issueToken` da
região. Falha ali é `Degraded`, e não `Unhealthy`: sem o motor a captura para, mas a
consulta continua acontecendo e o prontuário segue pelo caminho manual.

### `Storage:PublicBaseUrl`

A URL assinada que o motor recebe precisa ser **absoluta**: quem a consome está fora do
processo da API. Sem `Storage:PublicBaseUrl` a aplicação não sobe — despachar segmentos
com um caminho relativo produziria transcrições que nunca voltam, e a falha só
apareceria como uma consulta que não sai do lugar.

Em desenvolvimento, `https://localhost:7262` (o perfil `https`) resolve. Em produção é
a origem pública da API, ou o endpoint do bucket.

---

## 8. Autenticação

Todos os endpoints exigem JWT, exceto as rotas públicas de `api/auth`, os health checks e `/metrics`.

**Responsável (app)** — cadastro e login por e-mail e senha:

```bash
# 1. Cadastro — devolve token, refreshToken e consentimentoPendente
curl -X POST https://localhost:7262/api/auth/registro/tutor \
  -H "Content-Type: application/json" \
  -d '{"nome":"Ana","email":"ana@exemplo.com","telefone":"11999998888","senha":"senha-forte-123"}'

# 2. Login nas próximas vezes
curl -X POST https://localhost:7262/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"ana@exemplo.com","senha":"senha-forte-123"}'

# 3. Renovação — o refresh token rotaciona a cada uso
curl -X POST https://localhost:7262/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"..."}'
```

O access token vale **8 horas**; o refresh, **30 dias**, e é **rotativo**: cada renovação revoga o anterior. Reapresentar um token já usado derruba todas as sessões daquele usuário — é o sinal de que ele vazou.

**Veterinário** — o Admin cadastra o profissional em `POST /api/veterinarios`, e a resposta traz a **senha temporária** de primeiro acesso, exibida uma única vez. O veterinário entra em `/api/auth/login` com ela e troca em `POST /api/auth/trocar-senha`. Vet desativado ainda faz login, mas com a role `VetDesativado`, limitada ao extrato dos próprios atendimentos (RN-022/RN-024).

**Admin (desenvolvimento)** — o Admin ainda não tem cadastro próprio; a rota obsoleta segue disponível apenas em `Development`, e responde **404 fora dele**:

```bash
curl -X POST https://localhost:7262/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"usuario":"admin-teste","role":"Admin"}'
```

Roles: `Tutor`, `Veterinario`, `Admin` e `VetDesativado`. Policies: `ApenasAdmin`, `VeterinarioOuAdmin`, `ApenasTutor` e `TutorOuAdmin`. Use o token com `Authorization: Bearer {token}`.

O login responde **exatamente a mesma coisa** para e-mail inexistente, senha errada e conta desativada: distinguir os casos entregaria a lista de contas existentes. As senhas são guardadas com PBKDF2-HMAC-SHA256, 210.000 iterações e salt aleatório por senha.

**Cabeçalhos que valem conhecer:**

| Cabeçalho | Direção | Para que serve |
|---|---|---|
| `Authorization: Bearer {token}` | Requisição | Autenticação de usuário |
| `Idempotency-Key: {guid}` | Requisição | **Obrigatório** nas rotas marcadas como idempotentes. A resposta é reaproveitada por 24 h para a mesma chave |
| `X-Vetly-Service-Token` | Requisição | Autentica as rotas `api/internos/*` — quem chama é um provedor, não uma pessoa |
| `X-Correlation-Id` | Ambas | Envie o seu para amarrar a jornada; se não enviar, a API gera e devolve o dela |

---

## 9. Roteiro de testes no Postman

Esta é a **jornada completa da plataforma**, na ordem exata em que funciona — é o mesmo caminho que o teste `JornadaCompletaTests.JornadaFeliz_DoCadastroAAvaliacao` percorre por HTTP na suíte de integração. Cada passo traz o endpoint e o que ele faz. Base local: `https://localhost:7262`.

> **Antes de começar.** Crie no Postman as variáveis de ambiente `baseUrl`, `tokenTutor`, `tokenVet`, `tokenAdmin`, `tutorId`, `animalId`, `vetId`, `servicoId`, `slotId`, `consultaId`, `pagamentoId`, `referenciaExterna` e `documentoId`. Todo `POST` e `DELETE` das rotas idempotentes precisa do cabeçalho `Idempotency-Key` com um GUID novo a cada chamada — a chave protege contra o **mesmo** pedido reenviado, não contra dois pedidos diferentes.

### Bloco A — Preparar o prestador (perfil Admin, depois Veterinário)

| # | Método e rota | O que faz |
|---|---|---|
| A1 | `POST /api/auth/token` | Emite um JWT de Admin sem senha. **Só existe em `Development`** — é o atalho que o ambiente sem back-office oferece. Guarde em `tokenAdmin` |
| A2 | `POST /api/veterinarios` | Cadastra o profissional com CRMV, UF, persona e plano. Devolve o `id` do veterinário e a **senha temporária**, exibida uma única vez. Requer Admin (RN-107) |
| A3 | `POST /api/auth/login` | Autentica o veterinário com a senha temporária. Guarde em `tokenVet` |
| A4 | `PUT /api/veterinarios/{vetId}/agenda-config` | Define dias, horário, duração e intervalo, e **materializa 60 dias de horários**. Sem esta chamada não existe horário para o Responsável escolher (RN-034) |
| A5 | `PUT /api/veterinarios/{vetId}/servicos` | Define a vitrine de serviços com valor e duração. **É daqui que sai o preço cobrado** — nunca do corpo da requisição de pagamento (RN-032) |

```json
// A2 — POST /api/veterinarios   (Authorization: Bearer {{tokenAdmin}})
{ "nome": "Dra. Marina", "crmv": "12345-SP", "ufAtuacao": "SP",
  "email": "marina@clinica.com", "persona": 1, "plano": 2 }

// A4 — PUT /api/veterinarios/{{vetId}}/agenda-config   (Bearer {{tokenVet}})
{ "dias": [1,2,3,4,5], "horaInicio": "08:00", "horaFim": "18:00",
  "duracaoMinutos": 30, "intervaloMinutos": 0 }

// A5 — PUT /api/veterinarios/{{vetId}}/servicos   (Bearer {{tokenVet}})
{ "servicos": [ { "tipo": 1, "valor": 200.00, "duracaoMinutos": 30, "aceitaPlanoPet": false } ] }
```

### Bloco B — Onboarding do Responsável

| # | Método e rota | O que faz |
|---|---|---|
| B1 | `POST /api/auth/registro/tutor` | Cria a conta do Responsável e devolve a sessão com `consentimentoPendente: true`. Guarde `token` em `tokenTutor` e `tutorId` |
| B2 | `PUT /api/tutores/{tutorId}/consentimentos` | Concede a finalidade `Atendimento`. **Sem isto, toda rota de negócio responde 422**: a base legal precede o tratamento (RN-060) |
| B3 | `POST /api/animais` | Cadastra o pet. `pesoKg` é obrigatório — sem peso a IA não sugere dose (RN-081). A carteira de vacinação informada já **deriva o calendário de obrigações** (RN-046) |
| B4 | `POST /api/tutores/{tutorId}/dispositivos` | Registra o aparelho para receber push. Idempotente por token de push (RN-092) |

```json
// B1 — POST /api/auth/registro/tutor
{ "nome": "Ana Teste", "email": "ana@exemplo.com",
  "telefone": "11999998888", "senha": "senha-forte-123" }

// B2 — PUT /api/tutores/{{tutorId}}/consentimentos   (Bearer {{tokenTutor}})
{ "consentimentos": [ { "finalidade": "Atendimento", "concedido": true } ] }

// B3 — POST /api/animais   (Bearer {{tokenTutor}})
{ "nome": "Thor", "especie": "Canino", "raca": "Golden Retriever",
  "dataNascimento": "2023-04-10T00:00:00Z", "tutorId": "{{tutorId}}",
  "pesoKg": 31.5, "alergias": ["Dipirona"] }
```

### Bloco C — Encontrar e agendar

| # | Método e rota | O que faz |
|---|---|---|
| C1 | `GET /api/busca?animalId={animalId}&cep=01310-100&raioKm=10` | Lista clínicas e vets autônomos por proximidade e necessidade, ordenados pelo score 40/30/30. Espécie atendida é filtro **eliminatório** (RN-001 a RN-033) |
| C2 | `GET /api/veterinarios/{vetId}/disponibilidade` | Horários livres, por dia. Copie o `id` de um horário para `slotId` |
| C3 | `POST /api/consultas/checkout` | **Trava o horário por 10 minutos** e cria a consulta em `EmCheckout`. Quem chegar depois recebe 409 — é o que impede overbooking (RN-003/RN-035) |
| C4 | `POST /api/pagamentos` | Cria a cobrança com o split já apurado. Responde **202**, e o pagamento fica pendente: quem confirma é o webhook, nunca a resposta síncrona. Guarde `id` e `instrucoes.referenciaExterna` |
| C5 | `POST /api/internos/pagamentos/webhook` | Simula o provedor confirmando o pagamento. **Promove a consulta de `EmCheckout` para `Confirmada`** e ocupa o horário (RN-006). Usa `X-Vetly-Service-Token`, não JWT |
| C6 | `PUT /api/consultas/{consultaId}/pre-sintomas` | O Responsável descreve a queixa em texto guiado. É o único relato de quem convive com o animal, e alimenta o briefing (RN-005/RN-036) |

```json
// C3 — POST /api/consultas/checkout   (Bearer {{tokenTutor}}, Idempotency-Key)
{ "animalId": "{{animalId}}", "prestadorId": "{{vetId}}",
  "slotId": "{{slotId}}", "servicoId": "{{servicoId}}" }

// C4 — POST /api/pagamentos   (Bearer {{tokenTutor}}, Idempotency-Key)
{ "tutorId": "{{tutorId}}", "consultaId": "{{consultaId}}",
  "valor": 200.00, "meioPagamento": 1 }

// C5 — POST /api/internos/pagamentos/webhook   (X-Vetly-Service-Token: {{tokenInterno}})
{ "referenciaExterna": "{{referenciaExterna}}", "status": "Confirmado" }

// C6 — PUT /api/consultas/{{consultaId}}/pre-sintomas   (Bearer {{tokenTutor}})
{ "queixaPrincipal": "Vomito ha dois dias", "duracaoEmDias": 2,
  "sinaisObservados": ["Apatia"], "alimentacaoNormal": false }
```

> **Por que o valor vai no corpo se o servidor decide o preço?** Ele vai como declaração do cliente e é **ignorado**: o valor cobrado sai de `Servico.Valor`. Confira na resposta — ela devolve `200.00` mesmo que você mande `1.00`. Aceitar o valor do cliente é aceitar que ele pague o que quiser (RN-032).

### Bloco D — O atendimento (perfil Veterinário)

| # | Método e rota | O que faz |
|---|---|---|
| D1 | `GET /api/dashboard/veterinario` | O que precisa da atenção dele agora: agenda do dia, pendências que travam dinheiro ou documento, números do mês. Sem id na rota — o escopo vem do token (RN-105) |
| D2 | `GET /api/consultas/{consultaId}/briefing` | Contexto completo antes de começar: histórico, alergias, peso, medicações e os pré-sintomas já organizados (RN-005) |
| D3 | `POST /api/consultas/{consultaId}/iniciar` | **Abre a janela de captura** — a consulta começa aqui. Devolve os avisos que o profissional precisa ver antes, como peso ausente (RN-008) |
| D4 | `POST /api/consultas/{consultaId}/captura/segmentos` | Envia um trecho de áudio e enfileira a transcrição fora da requisição. Responde 202 (RN-009). *Opcional: planos Profissional e Enterprise* |
| D5 | `GET /api/consultas/{consultaId}/captura` | Situação da captura, com o texto já transcrito |
| D6 | `POST /api/consultas/{consultaId}/encerrar` | **Fecha a janela** e marca a consulta como `Realizada`. Encerrar não é finalizar (RN-008/RN-038) |
| D7 | `GET /api/consultas/{consultaId}/rascunho` | O prontuário que a IA estruturou a partir da transcrição — rascunho até o veterinário decidir (RN-080) |
| D8 | `PUT /api/consultas/{consultaId}/validar-diagnostico` | A decisão sobre o rascunho: `Aprovado`, `Corrigido` (exige o conteúdo corrigido) ou `NaoAprovado` (exige justificativa). Não há aprovação por omissão (RN-082) |
| D9 | `POST /api/consultas/{consultaId}/prontuario-manual` | O caminho sem IA: prontuário escrito à mão. É o que fecha o atendimento no plano Básico, ou quando o rascunho é recusado (RN-085) |

```json
// D9 — POST /api/consultas/{{consultaId}}/prontuario-manual   (Bearer {{tokenVet}})
{ "conteudo": {
    "anamnese": "Vomito ha dois dias, sem diarreia.",
    "exameFisico": "Hidratado, mucosas normocoradas, abdome sem dor a palpacao.",
    "hipotesesDiagnosticas": ["Gastrite alimentar"],
    "conduta": "Dieta branda por cinco dias.",
    "orientacoes": "Retornar se o vomito persistir." } }
```

### Bloco E — Documentos

| # | Método e rota | O que faz |
|---|---|---|
| E1 | `POST /api/documentos/consulta/{consultaId}?tipo=Prontuario` | Gera o documento pela Factory correspondente ao tipo, com conteúdo e PDF. Exige diagnóstico validado (RN-082/RN-083). Guarde o `id` |
| E2 | `POST /api/documentos/{documentoId}/assinar` | Assina pelo adaptador de assinatura. No MVP é o nome digitado, conferido contra o registrado — só o vet do atendimento assina (RN-087) |
| E3 | `POST /api/documentos/{documentoId}/publicar` | Publica no board do pet. **Receita sem assinatura não é publicada**: no board ela pareceria válida sem ser |
| E4 | `POST /api/consultas/{consultaId}/finalizar` | Fecho documental. Exige que todo documento **já emitido** que precise de assinatura esteja assinado (RN-087) |
| E5 | `GET /api/documentos/animal/{animalId}` | O board do pet, na visão do Responsável: documentos publicados do animal |
| E6 | `POST /api/documentos/{documentoId}/lido` | Registra que o Responsável abriu o documento |
| E7 | `POST /api/documentos/{documentoId}/correcao` | Cria uma **versão corrigida** — o original é preservado. Depois de 24 h exige justificativa (RN-088/RN-089) |

Tipos aceitos em `?tipo=`: `Prontuario`, `Receita`, `Atestado` (com `&subtipo=Saude`, `Obito` ou `Vacinacao`) e `NotaFiscal`.

### Bloco F — Depois da consulta

| # | Método e rota | O que faz |
|---|---|---|
| F1 | `POST /api/avaliacoes/consulta/{consultaId}` | O Responsável avalia. Só quem foi atendido avalia, uma vez por consulta e em até **14 dias** (RN-055) |
| F2 | `GET /api/avaliacoes/veterinario/{vetId}` | Reputação com distribuição de notas. Abaixo de 3 avaliações a nota não é pública nem entra no score (RN-057) |
| F3 | `POST /api/avaliacoes/{avaliacaoId}/resposta` | A resposta pública do veterinário — uma só |
| F4 | `GET /api/fidelidade/saldo` | Saldo, tier e o que vence em 30 dias. Serviço pago rende 1 ponto por real; obrigação cumprida no prazo rende 50 fixos (RN-047/RN-048) |
| F5 | `POST /api/fidelidade/resgates/simular` | Mostra o desconto em reais e **como o custo se divide entre Vetly e prestador**, sem gravar nada (RN-051) |
| F6 | `POST /api/fidelidade/resgates` | Debita os pontos em FIFO e emite o cupom com QR e 30 dias de validade (RN-050/RN-053) |
| F7 | `GET /api/notificacoes/tutor/{tutorId}` | A caixa de entrada do Responsável — sobrevive ao push perdido (RN-092) |
| F8 | `GET /api/animais/{animalId}/board` | O board do pet: obrigações, agendamentos, documentos e o estado do avatar |
| F9 | `POST /api/consultas/{consultaId}/retorno` | Agenda o retorno. Nasce **confirmado e sem cobrança nova** — é a segunda metade de um tratamento já pago (RN-013) |

### Bloco G — Os caminhos que custam dinheiro

Estes são os cenários que valem testar depois do caminho feliz, porque são onde as regras aparecem:

| # | Método e rota | O que provar |
|---|---|---|
| G1 | `GET /api/consultas/{id}/simulacao-cancelamento` | O valor do reembolso **antes** de cancelar, pela mesma Strategy que o cancelamento usa. Mostrar um valor e cobrar outro é o que a RN-042 proíbe |
| G2 | `DELETE /api/consultas/{id}` | Cancela e aplica a faixa: **> 24 h** reembolso integral; **entre 24 h e 2 h** parcial com a retenção da clínica; **< 2 h** sem reembolso (RN-041/RN-042) |
| G3 | `POST /api/consultas/{id}/remarcar` | Transfere horário e pagamento sem nova cobrança. **Limite de 2** — acima disso, remarcar viraria burla à janela de reembolso (RN-043) |
| G4 | `POST /api/consultas/{id}/no-show` | Registra o não comparecimento. Só quem esperava registra — nunca o próprio Responsável — e não gera reembolso (RN-044) |
| G5 | `POST /api/consultas/checkout` **duas vezes no mesmo `slotId`** | A segunda recebe **409**. É a prova de que a concorrência é resolvida no banco, não em memória (RN-035) |
| G6 | `POST /api/internos/pagamentos/webhook` com `"status": "Recusado"` | Expira a consulta, libera o horário e **devolve o cupom à vigência** — o desconto não foi usado (RN-053) |
| G7 | `POST /api/pagamentos` repetindo o mesmo `Idempotency-Key` | A segunda chamada devolve a **mesma resposta**, sem criar uma segunda cobrança |
| G8 | `GET /api/consultas/{id}` com o token de **outro** Responsável | **403 com `RN-105`**. O escopo vem do token, nunca de parâmetro do cliente |
| G9 | `GET /api/financeiro/consolidado` | O painel do Admin. O campo `fecha` confirma a conta `bruto = comissão + repasse + desconto` |
| G10 | `POST /api/colmeia` | O Responsável — e só ele — autoriza um veterinário de fora a alcançar o histórico, com escopo e prazo (RN-090) |

### Bloco H — Observabilidade (sem autenticação)

| # | Método e rota | O que faz |
|---|---|---|
| H1 | `GET /health/live` | O processo está no ar? Não toca dependência nenhuma |
| H2 | `GET /health/ready` | As dependências respondem? 503 tira a instância de rotação |
| H3 | `GET /health` | Relatório completo, com duração e tags de cada check |
| H4 | `GET /metrics` | Todas as métricas no formato Prometheus. Faça algumas chamadas antes e procure por `vetly_http_requisicoes_total` e `vetly_regras_violadas_total` |

**Um teste de correlação que vale fazer no Postman:** envie qualquer requisição com o cabeçalho `X-Correlation-Id: meu-teste-123`. A resposta volta com o mesmo cabeçalho; se a requisição falhar, o corpo do erro traz `"correlationId": "meu-teste-123"`; e `grep 'meu-teste-123' src/Vetly.API/logs/vetly-*.log` mostra **todas** as linhas daquela requisição, de todas as camadas.

---

## 10. Referência de endpoints

Todas as rotas da API, agrupadas por área, com uma frase do que cada uma faz. Para
exercitá-las na ordem em que a jornada acontece, use o [roteiro do Postman](#9-roteiro-de-testes-no-postman).

### Observabilidade
Públicos, sem autenticação. Detalhados na [seção 4](#4-monitoramento-e-observabilidade).

| Método | Rota | Descrição |
|---|---|---|
| GET | `/health/live` | Liveness — só verifica se o processo está no ar; não toca em dependências. Decide **reiniciar** o container |
| GET | `/health/ready` | Readiness — verifica Oracle e Ollama. Decide se a instância recebe **tráfego** |
| GET | `/health` | Diagnóstico completo — todos os checks registrados, com duração e tags |
| GET | `/metrics` | Métricas no formato de texto do Prometheus: plataforma, runtime e negócio |

Checks registrados: `api` (tag `live`), `oracle-db` (tags `ready,db,oracle`) e `ollama` (tags `ready,external`). `Healthy` e `Degraded` respondem **200**; `Unhealthy` responde **503**.

```bash
curl http://localhost:5140/health/ready
curl http://localhost:5140/metrics | grep '^vetly_'
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
| GET | `/api/animais/{id}/board` | Board do pet: obrigações, agendamentos, documentos, avatar (RN-011/RN-020) |
| GET | `/api/animais/{id}/obrigacoes` | Calendário de obrigações, pela navegação do pet (RN-045) |
| GET | `/api/animais/{id}/acessos` | Quem leu o histórico do animal (RN-067) |
| GET | `/api/animais/{id}/prontuarios` | Histórico longitudinal de prontuários |
| GET | `/api/animais/{id}/exames` | Exames do animal |
| POST | `/api/animais` | Cadastrar — exige `pesoKg`; aceita sexo, castrado, alergias, condições pré-existentes e carteira de vacinação |
| PUT | `/api/animais/{id}` | Atualizar — mesmos campos do cadastro |
| PUT | `/api/animais/{id}/peso` | Registra o peso aferido no atendimento (RN-081) |
| PATCH | `/api/animais/{id}/historico/{registroId}/ocultar` | Esconde um registro do board do Responsável (RN-068) |
| DELETE | `/api/animais/{id}` | Desativar (soft delete) |

**Alterar o cadastro é do dono; ler o prontuário, de quem atende.** São escopos distintos, e confundi-los deixaria um veterinário que atendeu uma vez renomear o pet ou desativá-lo. **O peso é a exceção prevista** e tem caminho próprio: o profissional o afere durante a consulta, e sem ele a IA não sugere dose (RN-081).

**Ocultar não é apagar (RN-068).** O registro sai do board do Responsável e continua existindo para o veterinário, o Admin e a auditoria — um histórico que some da vista de quem prescreve seria perigoso, não discreto. **Registro que menciona alerta ativo ou alergia do animal não é ocultável:** esconder uma alergia do próprio dono é o oposto do que o board existe para fazer, e o risco aparece quando o animal chega desacordado num plantão que não é o de sempre. A guarda vale só na direção de esconder — voltar a exibir é sempre aceito.

O board já nasce preenchido: `POST /api/animais` deriva as obrigações da carteira de vacinação informada (RN-046). Cadastrar o pet e informar as vacinas é, para quem cadastra, a mesma ação. Falha na derivação não desfaz o cadastro — perder o pet recém-cadastrado por causa do board seria trocar o essencial pelo acessório.

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
| GET | `/api/tutores/{id}/carteira` | Pagamentos, descontos e reembolsos (RN-041/RN-071) |
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
| PUT | `/api/consultas/{id}/pre-sintomas` | Texto guiado + mídias, antes do atendimento (RN-005/RN-036) |
| GET | `/api/consultas/{id}/simulacao-cancelamento` | O valor do reembolso **antes** de cancelar (RN-014/RN-042) |
| POST | `/api/consultas/{id}/remarcar` | Transfere o horário sem nova cobrança — limite 2 (RN-013/RN-043) |
| POST | `/api/consultas/{id}/no-show` | Registra não comparecimento, sem reembolso (RN-044) |
| POST | `/api/consultas/{id}/iniciar` | Abre a janela de captura — a consulta começa aqui (RN-008) |
| POST | `/api/consultas/{id}/captura/segmentos` | Recebe um trecho de áudio e enfileira a transcrição — 202 (RN-009) |
| GET | `/api/consultas/{id}/captura` | Situação da captura, com o texto já transcrito (RN-009) |
| POST | `/api/consultas/{id}/encerrar` | Fecha a janela de captura e marca a consulta como `Realizada` (RN-008/RN-038) |
| GET | `/api/consultas/{id}/rascunho` | Prontuário estruturado pela IA — rascunho até o vet decidir (RN-080/RN-082) |
| PUT | `/api/consultas/{id}/validar-diagnostico` | Decisão sobre o rascunho: `Aprovado`, `Corrigido` ou `NaoAprovado` (RN-082) |
| POST | `/api/consultas/{id}/prontuario-manual` | Prontuário escrito à mão, sem IA no caminho (RN-085) |
| GET | `/api/consultas/{id}/auditoria-ia` | Trilha append-only das decisões sobre conteúdo de IA (RN-082) |
| POST | `/api/consultas/{id}/finalizar` | Fecho documental — exige o que já foi emitido assinado (RN-087) |
| POST | `/api/consultas/{id}/retorno` | Agenda o retorno, confirmado e sem cobrança nova (RN-013) |
| GET | `/api/consultas/{id}/redistribuicao/candidatos` | Veterinários que poderiam assumir a consulta (RN-025) |
| POST | `/api/consultas/{id}/redistribuir` | Passa a consulta a outro veterinário (RN-025) |
| DELETE | `/api/consultas/{id}` | Cancelar + Strategy de reembolso (RN-014/RN-041/RN-042) |

**Redistribuir em vez de cancelar em massa (RN-025).** Quando o profissional sai da plataforma ou fica indisponível, cancelar jogaria o problema no colo do Responsável, que agendou de boa-fé e teria de refazer tudo — inclusive pagar de novo. A redistribuição preserva pagamento, animal e compromisso; o que muda é quem atende.

Os candidatos vêm ordenados pela **proximidade do horário original**, não por reputação: quem agendou às 14h de terça organizou o dia em torno disso, e trocar o profissional já é uma quebra. Espécie é eliminatória (RN-029). O horário novo é travado antes de a consulta ser movida — sem isso, duas redistribuições simultâneas mandariam dois animais para o mesmo slot — e o antigo volta à disponibilidade.

O Responsável é avisado, e o `motivo` é obrigatório porque entra na mensagem: aviso sem motivo soa como erro do app. Restrito à administração — nem o veterinário que sai decide para quem vai.

**Os pré-sintomas são texto guiado, não campo livre** (RN-036): perguntas fechadas produzem contexto que o veterinário lê em dez segundos no briefing e que a IA consegue usar; um parágrafo solto não faz nem uma coisa nem outra. Só valem antes do atendimento — depois, o briefing já foi lido.

**A simulação de cancelamento usa a mesma seleção de Strategy do cancelamento**, aplicada só para calcular. Se usasse outro critério, mostraria um valor e o cancelamento cobraria outro — que é exatamente o que a RN-042 quer evitar ao exigir que a política de retenção seja transparente no agendamento.

**Remarcar tem limite de 2 por consulta** (RN-043): duas cobrem imprevisto legítimo, e acima disso remarcar vira burla à janela de reembolso — quem quer desistir sem perder dinheiro empurraria a data indefinidamente em vez de cancelar sob a política. O pagamento acompanha a nova data (RN-013), o horário novo é travado antes da troca e o antigo volta à fila de espera.

**A janela de captura é explícita.** `iniciar` abre, `encerrar` fecha, e fora dela a IA não captura áudio nem produz conteúdo clínico (RN-079) — trecho enviado com a janela fechada devolve 409. O áudio vai em segmentos curtos, cada um com sua sequência: assim a transcrição acontece durante o atendimento e a falha de um trecho não derruba a consulta inteira. Reenvio da mesma sequência devolve 409, porque duplicaria o texto.

O despacho ao motor sai da requisição pelo worker: o veterinário não espera a transcrição para continuar atendendo. O motor devolve o texto pelo contrato de `POST /api/internos/stt/callback`, e **esse contrato é da Vetly, não do motor** — é o que permite trocar de fornecedor sem refazer o caminho de volta. Em desenvolvimento, `Adaptadores:Stt = "Simulado"` percorre o mesmo caminho assíncrono com texto sintético marcado como tal. O passo a passo completo está em [§7](#7-fluxo-de-captura-da-consulta).

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

### Analytics

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/analytics/plataforma` | Funil, uso da IA e receita do período (`?inicio=&fim=`) (RN-106) |

São três perguntas, e as seções respondem uma cada: **o agendamento está virando atendimento? o dinheiro está entrando? a IA está ajudando ou dando trabalho?**

No funil, as taxas importam mais que os absolutos — 30 cancelamentos em 1000 consultas é ruído; em 60, é problema de agenda — e cada taxa tem o denominador certo: conversão sobre o que foi criado, cancelamento sobre o que chegou a ser pago (contar a consulta expirada faria a taxa parecer melhor do que é).

Em `ia`, a métrica que interessa não é quantos rascunhos foram gerados: é quantos o veterinário **aceitou sem corrigir**. Correção alta significa que a IA está dando trabalho em vez de poupar; recusa alta, que ela erra o suficiente para não ser confiável. Prontuário manual fica fora do denominador — não é rascunho recusado, é atendimento que nunca teve rascunho. O dado vem da trilha append-only da RN-082, que existia justamente para isso.

Nenhum número identifica pessoa: analytics é agregado, e cruzar métrica com dado de Responsável ou de animal seria usar a base clínica para outra coisa.

### Financeiro (administração)

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/financeiro/consolidado` | Consolidado do período, com repasse por destinatário (RN-070/RN-072) |
| POST | `/api/financeiro/liquidar` | Marca os repasses do período como liquidados (RN-071) |

**A conta que este painel precisa fechar é uma só: `bruto = comissão + repasse + desconto`.** O campo `fecha` diz se ela bate — split incoerente é silencioso, os totais continuam somando e só a conta cruzada revela o problema. `porDestinatario` é a lista que a operação usa para pagar, ordenada pela maior pendência: um prestador, um valor.

A liquidação acontece **fora da plataforma** (RN-071) — transferência, lote do banco. A rota registra que aconteceu, e por isso a `referencia` é obrigatória: marcar como pago sem dizer com base em quê deixa a conferência sem âncora. Pagamento já liquidado é ignorado, não recontado — a operação repete fechamento com frequência, e chamar duas vezes não pode pagar duas vezes; a resposta diz quantos foram ignorados. Cobrança pendente ou recusada nunca entra em fechamento.

Restrito à administração: o veterinário vê o próprio dinheiro pelo extrato (RN-024), e o Responsável pelos próprios pagamentos.

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
| GET | `/api/notificacoes/preferencias` | O que o Responsável escolheu receber (RN-093) |
| PUT | `/api/notificacoes/preferencias` | Liga ou desliga as comunicações promocionais (RN-093) |
| POST | `/api/notificacoes/{id}/lida` | Registra que o Responsável abriu no app |

**Promoção é opt-in, e é a única preferência que existe (RN-093).** Aviso de consulta, documento publicado e obrigação vencendo são o serviço contratado — oferecê-los como preferência faria o app poder deixar de avisar sobre a saúde do animal. A escolha é gravada no registro de consentimento de LGPD, e não numa coluna própria: duas fontes para a mesma vontade acabariam discordando, e a que vale juridicamente é o consentimento. O escopo vem do token; não há parâmetro de Responsável nessas rotas, e não pode haver.

**A notificação é gravada antes de ser enviada.** O app precisa de uma caixa de entrada que sobrevive ao push perdido — aparelho desligado, token trocado, permissão negada — e o histórico do que foi comunicado é o que permite responder "avisamos?" depois. `NaoEntregue` não é o fim da linha: a notificação segue visível na caixa, porque push perdido não pode significar aviso perdido.

O envio sai da requisição, numa rotina de um minuto: o Responsável não pode esperar o APNs responder para que a consulta seja confirmada. Token que o provedor recusa como inválido **desativa o dispositivo** — app desinstalado e token rotacionado são o caso comum, não a exceção; falha do provedor, ao contrário, não desativa nada. O push passa por `IPushAdapter`, escolhido por `Adaptadores:Push`.

**A régua de lembretes** (rotina diária) transforma obrigação vencendo em aviso: sem ela, o board de obrigações é uma tela que só quem abre o app descobre — e quem já esqueceu da vacina é exatamente quem não abre. É **um aviso por animal, não por obrigação**, e nomeia a mais urgente em vez de dizer "você tem pendências", porque aviso genérico não move ninguém. Há intervalo mínimo de 7 dias entre dois avisos do mesmo animal: avisar de hora em hora sobre a mesma vacina transformaria cuidado em incômodo, e o Responsável desligaria a notificação inteira. Cada aviso cria também o `LembreteAgendado` que sustenta a régua — três tentativas sem resposta acionam o alerta à clínica (RN-095).

**A régua avança sozinha nos marcos de 7, 3 e 1 dia** antes do evento (rotina `AgendarTentativasDaRegua`). Sem ela, a régua nascia e parava: o alerta à clínica depende de três tentativas, e as tentativas nunca aconteciam. Os marcos são decrescentes de propósito — sete dias é planejamento, três é lembrete, um dia é urgência; três avisos iguais na mesma semana teriam a mesma frequência com menos utilidade. É **um degrau por execução**, para que uma régua que ficou parada não vire três notificações no mesmo minuto. Evento já vencido continua avançando: é aí que a régua mais importa.

### Avaliações

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/avaliacoes/pendentes` | Atendimentos esperando avaliação, com o prazo (RN-055) |
| POST | `/api/avaliacoes/consulta/{id}` | Avalia um atendimento realizado (RN-055) |
| GET | `/api/avaliacoes/veterinario/{id}` | Reputação, com distribuição das notas (RN-057) |
| POST | `/api/avaliacoes/{id}/resposta` | Resposta pública do veterinário — uma só |
| POST | `/api/avaliacoes/{id}/moderar` | Esconde o comentário; a nota continua contando |

**Só avalia quem foi atendido, e só uma vez por consulta.** É o que separa reputação de campanha: sem o vínculo com um atendimento realizado, a nota vira número que qualquer um pode empurrar. O prazo é de **14 dias** — a janela do Airbnb, referência de marketplace bilateral com reputação; avaliação muito posterior mede memória, não atendimento. A nota não é editável depois de enviada, porque corrigir avaliação abriria a porta para pressão sobre quem avaliou. O índice único em `CONSULTA_ID` é a invariante: sem ele, duas requisições simultâneas passariam pela verificação e gravariam as duas.

**Cancelar uma consulta invalida a avaliação dela (RN-059)**, mas não a apaga: a nota sai do cálculo e a linha permanece com o motivo. A diferença importa — deletar permitiria a um prestador limpar uma avaliação ruim provocando o cancelamento, e a auditoria não teria como notar.

A **moderação esconde o comentário e preserva a nota**. O contrário transformaria a moderação em ferramenta para apagar crítica, e há um teste que fixa isso. Moderar exige motivo: moderação sem motivo não se audita.

A reputação em `TB_VETERINARIO` é **recalculada** a partir das avaliações, não incrementada — média acumulada em campo diverge do que está gravado assim que uma avaliação é moderada ou corrigida. Abaixo de 3 avaliações a nota não é pública nem entra no score (RN-057): uma nota 5 vinda de uma única avaliação não diz nada sobre o profissional, e o matching usa o selo "Novo na Vetly" nesse intervalo (RN-033).

### Fidelidade

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/fidelidade/saldo` | Saldo, tier e o que vence em 30 dias (RN-048/RN-050) |
| GET | `/api/fidelidade/extrato` | Extrato dos lançamentos (RN-047 a RN-052) |
| POST | `/api/fidelidade/resgates/simular` | Desconto e divisão do custo, sem gravar (RN-017/RN-051) |
| POST | `/api/fidelidade/resgates` | Debita em FIFO e emite o cupom (RN-018/RN-050/RN-053) |
| GET | `/api/fidelidade/cupons` · `/{id}` | Cupons do Responsável (RN-053) |

O cupom é aplicado no checkout: `POST /api/pagamentos` aceita `cupomId`.

**Os parâmetros são fechados** (`vetly-tech` §1): serviço pago rende **1 ponto por R$ 1**; obrigação cumprida **no prazo** rende **50 pontos fixos** — é o bônus que paga comportamento de cuidado, não gasto, e por isso cumprir atrasado resolve a pendência mas não credita. Ambos passam pelo multiplicador do tier: Bronze 1,0× · Prata 1,25× · Ouro 1,5×, sobre o acúmulo de 12 meses. **100 pontos valem R$ 3,00.**

O tier conta o que foi **creditado** na janela, não o saldo: quem resgatou não perde a faixa por ter usado o programa — usar é exatamente o comportamento que o programa quer.

**O saldo é a soma dos lançamentos, não um campo guardado.** Saldo à parte diverge do extrato no primeiro erro. O crédito é um **lote** com saldo próprio, porque o consumo é **FIFO** (RN-050): o resgate come primeiro o ponto mais antigo, que é o que está mais perto de vencer. Sem isso, "expirar o que venceu" e "gastar o mais velho" seriam a mesma conta feita de dois jeitos incompatíveis. Ponto já gasto não expira de novo, e o estorno de cancelamento (RN-052) só tira o que ainda não foi usado — cobrar de volta ponto resgatado de boa-fé deixaria o saldo negativo.

**Quem paga o desconto depende do tamanho dele (RN-051).** Até R$ 10 a Vetly banca sozinha, o que preserva a adesão do vet ao programa; de R$ 10,01 a R$ 30 a divisão é 60/40; acima de R$ 30, 30/70 — resgate grande é co-financiado por quem captura a recorrência. A parte da Vetly sai da comissão, a do prestador sai do repasse, e o bruto continua sendo o preço do serviço: **comissão + repasse + desconto = valor**.

O resgate emite um **cupom** com código QR e 30 dias de validade (RN-053). Vencido, os pontos **não** voltam ao saldo — é o que evita passivo perpétuo e resgate especulativo. Um cupom vale para uma transação (RN-054). A validação física no estabelecimento não existe no MVP (RN-019).

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
| POST | `/api/ia/estruturar` | Estrutura uma transcrição em prontuário (RN-080) |
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

---

## 11. Correções de segurança

Uma revisão completa do fluxo de comunicação da API encontrou brechas em que a regra
existia no documento e não no código, ou existia num serviço e não no outro. O que
segue é o que mudou e por quê — cada item tem teste que falha se a correção for
desfeita.

### Escopo por linha (RN-105/RN-106)

O conflito **C-07** — qualquer usuário autenticado listava dados de todo mundo — foi
fechado na onda 2 para tutores, animais, consultas e pagamentos. A revisão encontrou
oito rotas que a correção não alcançou:

- **Exames** chegavam ao Responsável sem passar pela liberação do veterinário
  (RN-104), tanto em `GET /api/exames` quanto em `GET /api/animais/{id}/exames`. Ler o
  próprio exame não liberado responde **403 com RN-104**, e não 404: o Responsável sabe
  que o exame existe — foi ele quem o pediu; o que ainda não existe para ele é o
  resultado interpretado.
- **Internações** eram abertas e encerradas por qualquer autenticado.
- **Empresas** eram criadas sem passar pelo Admin — uma clínica entraria no matching
  sem validação nenhuma.
- **Lembretes** eram agendados por qualquer profissional sobre o pet de outro, e a
  régua termina em push no telefone do Responsável.
- **A agenda do veterinário** vinha do id da rota: bastava trocar um Guid na URL para
  ler a agenda alheia. O id passa a vir do token.
- **Documentos** eram lidos e emitidos sem guarda; agora passam por escopo com
  fallback de colmeia, e **toda leitura por veterinário vira log de acesso, inclusive a
  do próprio autor** — registrar só o acesso "de fora" deixaria metade da história fora
  do registro que sustenta a colmeia juridicamente (RN-067).
- **O plano do veterinário** podia ser trocado pelo próprio: o plano decide a comissão
  (RN-070), e isso era deixá-lo baixar a própria comissão. Passa a ser ato do Admin.
- **O cadastro do animal** era editável por quem o atendia. Ler prontuário é trabalho
  clínico; reescrever cadastro não é.

### Concorrência no horário (RN-035)

A guarda em memória do slot resolvia a corrida dentro de uma requisição, não entre
duas: dois processos liam o mesmo horário `Livre` no mesmo milissegundo, os dois
passavam pela guarda e o último a gravar vencia — dois animais no mesmo horário.
`ESTADO` e `LOCK_CONSULTA_ID` viraram **tokens de concorrência**, e a colisão é
traduzida em **409** na fronteira do repositório, para que a camada de aplicação siga
sem conhecer o ORM.

O webhook de pagamento passa a mexer **apenas no slot que a consulta está segurando**.
Ele é assíncrono e pode chegar depois de o lock expirar: confirmar ali daria o horário
ao pagamento atrasado, e liberar ali derrubaria a reserva de quem chegou legitimamente
depois — nos dois casos a vítima é quem não errou nada.

### A cobrança deixa de aceitar o que o cliente manda (RN-006/RN-032)

`POST /api/pagamentos` aceitava do cliente as três coisas que o servidor tem de decidir
sozinho: **quem paga, quanto paga e por qual atendimento**. Passa a validar tudo antes
de qualquer coisa sair para o provedor — o Responsável vem do token, a consulta tem de
existir, ser dele e estar em `EmCheckout` ou `Confirmada`, não pode haver outra cobrança
em aberto, o lock do horário tem de continuar valendo, e **o valor vem de
`Servico.Valor`, nunca do corpo**. Aceitar o valor do cliente é aceitar que ele pague o
que quiser.

O cupom ganhou dois limites: a parte da Vetly não pode passar da própria comissão — um
cupom grande numa consulta barata produzia comissão negativa, a plataforma pagando para
que a consulta acontecesse (RN-051) — e cupom não se aplica a internação, que não passa
pelo split que financia o desconto. Pagamento recusado **devolve o cupom à vigência**:
o desconto não foi usado, e manter o cupom queimado cobraria os pontos por um benefício
que ninguém recebeu (RN-053).

### O callback de transcrição prova de qual trecho está respondendo (RN-009)

O token de serviço no cabeçalho autentica o fluxo de transcrição como um todo.
`SegmentoAudio.CallbackTokenHash` era gravado no despacho e **nunca conferido**: quem
conhecesse o token de serviço podia escrever texto no prontuário de qualquer consulta,
bastando acertar um id de segmento. O callback passa a exigir o token daquele segmento,
comparado em **tempo fixo** — comparar hash com `==` vaza pelo tempo de resposta quantos
caracteres iniciais estavam certos, e quem pode repetir a chamada transforma isso em
adivinhação caractere a caractere.

### A IA não contorna o consentimento (RN-064/RN-066)

O contexto da estruturação passa a levar pré-sintomas, alertas de segurança e histórico
— este último **pelo mesmo filtro de colmeia da leitura humana**. Uma IA que lesse o
histórico inteiro quando o profissional não pode lê-lo seria uma forma indireta de
contornar o consentimento: o texto voltaria ao veterinário dentro do rascunho, sem nunca
ter passado pela guarda. O acesso da IA também vira log — quem lê em nome do
veterinário continua sendo o veterinário.

### Encerrar e finalizar deixam de ser o mesmo evento (P-01/RN-087)

`EncerrarAsync` marcava `Realizada` **e** `Finalizada` de uma vez. A consulta nascia
finalizada com documento nenhum emitido, e a exigência da RN-087 nunca chegava a ser
cobrada de verdade — no instante em que era avaliada, não havia documento algum. São
dois momentos: encerrar é o profissional dizendo que terminou de atender; finalizar é o
fim do trabalho documental que vem depois.

### Higiene de dependências

`Microsoft.OpenApi` 2.0.0, trazida por transitividade pelo
`Microsoft.AspNetCore.OpenApi`, tem CVE aberto (GHSA-v5pm-xwqc-g5wc) e foi **pinada
acima da versão vulnerável** por referência direta — a única forma de subir uma
dependência indireta. `dotnet build -warnaserror` fica limpo, e é assim que ele deve
continuar: o pass com `-warnaserror` foi o que revelou `TutoresController._pagamentos`
nunca atribuído, um `NullReferenceException` esperando em
`GET /api/tutores/{id}/carteira`.

### O que continua valendo

Credencial, connection string e chave JWT **não vão para o repositório**: o arranjo com
`appsettings.Development.local.json` no `.gitignore` segue sendo o caminho.
`POST /api/auth/token` emite JWT sem senha e por isso responde **404 fora de
Development**, marcada `[Obsolete]` — emitir token sem credencial em produção seria uma
porta aberta. O login responde **exatamente a mesma coisa** para e-mail inexistente,
senha errada e conta desativada: distinguir os casos entregaria a lista de contas
existentes.

---

## 12. Modelo entidade-relacionamento

As tabelas principais e seus campos. Os nomes seguem a convenção Oracle em maiúsculas,
definida nas classes `IEntityTypeConfiguration<T>` da Infrastructure — o Domain não
conhece nenhum deles. Identificadores são `CHAR(36)` guardando GUID em texto, decisão
que troca alguns bytes por legibilidade em consulta manual e por portabilidade entre
bancos.

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

---

## Regras de negócio

O catálogo completo — **RN-001 a RN-107**, com a descrição de cada regra e a classe que a
implementa — vive em um documento separado:

### 📘 **[REGRAS-DE-NEGOCIO.md](REGRAS-DE-NEGOCIO.md)**

A separação é deliberada. Este README responde *"o que a API faz e como operá-la"*, e é o
documento de quem chega para integrar ou para colocar o sistema no ar. O catálogo de regras
responde *"por que ela se comporta como se comporta"*, e é o documento de quem chega para
alterar o comportamento. Misturar os dois produziria um arquivo que ninguém lê inteiro e no
qual ninguém encontra o que veio buscar.

O código que aparece no campo `codigo` de toda resposta de erro é uma chave daquele
catálogo — e é também a tag da métrica `vetly_regras_violadas_total`. Da resposta HTTP à
regra, e da regra ao arquivo que a implementa, sem intermediários.

---

## Licença e contexto

Projeto acadêmico desenvolvido para a disciplina **Advanced Business Development with .NET**.
O documento de produto (`vetly-produto.md`) e o documento técnico (`vetly-tech.md`) são as
fontes das regras referenciadas como `RN-xxx` ao longo desta documentação.
