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
| Testes | xUnit + Moq (89 testes verdes) |

## Padrões aplicados

| Padrão | Onde |
|---|---|
| Factory Pattern | `DocumentoService` seleciona `IDocumentoFactory` pelo `TipoDocumento` (Prontuario, Receita, Atestado, NotaFiscal) |
| Strategy Pattern | `ConsultaService` seleciona `ICancelamentoStrategy` por antecedência (RN-014/RN-041/RN-042) |
| Strategy Pattern | `PagamentoService` seleciona `ISplitFinanceiroStrategy` pela `PersonaVeterinario` (autônomo vs. vinculado) |
| Repository Pattern | Interfaces em `Vetly.Application`; implementações EF Core em `Vetly.Infrastructure` |
| DIP | Todos os serviços dependem de interfaces — zero acoplamento concreto |
| Soft Delete | `Veterinario`, `Animal` e `Tutor` são desativados, nunca deletados |
| Value Object | `Crmv` — imutável, valida regex `^\d{4,6}-[A-Z]{2}$` |
| ProblemDetails | `ExceptionHandlingMiddleware` retorna RFC 7807 em todos os erros |
| Enums como string | `JsonStringEnumConverter` — o JSON trafega `"Presencial"`, não `1` (entrada e saída) |
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

Todos os endpoints exigem JWT, exceto `POST /api/auth/token`.

```bash
curl -X POST https://localhost:7262/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"usuario":"admin-teste","role":"Admin"}'
```

Roles disponíveis: `Admin` e `Veterinario`. Endpoints de criação e desativação de veterinários exigem role `Admin` (policy `ApenasAdmin`). Use o token retornado com `Authorization: Bearer {token}` nas demais chamadas.

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
| POST | `/api/auth/token` | Gera token JWT (público — sem autenticação) |

### Veterinarios
| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/veterinarios` | Lista todos ativos |
| GET | `/api/veterinarios/{id}` | Detalhe |
| GET | `/api/veterinarios/regiao/{uf}` | Por UF (ex: SP, RJ) |
| GET | `/api/veterinarios/{id}/agenda` | Agenda futura de consultas |
| POST | `/api/veterinarios` | Cadastrar — requer role Admin (RN-107); aceita bloco de endereço (RN-026) |
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
| GET | `/api/tutores` | Lista todos ativos |
| GET | `/api/tutores/{id}` | Detalhe |
| GET | `/api/tutores/{id}/animais` | Animais do tutor |
| POST | `/api/tutores` | Cadastrar |
| PUT | `/api/tutores/{id}` | Atualizar |
| DELETE | `/api/tutores/{id}` | Desativar (soft delete + anonimização LGPD) |

### Consultas
| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/consultas` | Lista com filtros: `dataInicio`, `dataFim`, `veterinarioId`, `cancelada` |
| GET | `/api/consultas/{id}` | Detalhe |
| GET | `/api/consultas/veterinario/{id}` | Por veterinário |
| GET | `/api/consultas/animal/{id}` | Por animal |
| GET | `/api/consultas/{id}/briefing` | Pré-consulta: animal, histórico (últimas 5) e exames recentes (últimos 3) |
| POST | `/api/consultas` | Agendar — requer pagamento confirmado (RN-006) |
| PUT | `/api/consultas/{id}/validar-diagnostico` | Registra validação manual do diagnóstico (RN-082) |
| POST | `/api/consultas/{id}/finalizar` | Finalizar — exige receita assinada (RN-087) |
| DELETE | `/api/consultas/{id}` | Cancelar + Strategy de reembolso (RN-014/RN-041/RN-042) |

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
| POST | `/api/documentos/consulta/{id}?tipo={TipoDocumento}` | Gerar via Factory — exige diagnóstico validado (RN-082) |
| POST | `/api/documentos/{id}/assinar` | Assinar digitalmente (RN-087) |
| POST | `/api/documentos/{id}/correcao` | Criar versão corrigida — após 24h exige justificativa (RN-088/RN-089) |

### Pagamentos
| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/pagamentos` | Lista todos |
| GET | `/api/pagamentos/{id}` | Detalhe |
| POST | `/api/pagamentos` | Registrar pagamento |
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

### IA (Ollama)
| Método | Rota | Descrição |
|---|---|---|
| POST | `/api/ia/diagnostico` | Sugerir hipóteses diagnósticas |
| POST | `/api/ia/protocolo` | Sugerir protocolo de tratamento |
| POST | `/api/ia/triagem` | Triar sintomas por urgência |
| POST | `/api/ia/orientacoes` | Orientações pós-atendimento para o tutor |

> **Todas as respostas da IA são sugestões — o veterinário deve validar manualmente antes de gerar qualquer documento clínico (RN-082).**

---

## Regras de Negócio

> A numeração segue o documento técnico oficial (`vetly-tech.md`, RN-001 a RN-107). As versões anteriores deste
> README usavam uma numeração própria que colidia com códigos diferentes do documento técnico — o de-para foi
> aplicado ao código, às exceções e a esta tabela.

| Código | Descrição | Implementação |
|---|---|---|
| RN-006 | Consulta só pode ser agendada se o pagamento estiver com status Confirmado | `ConsultaService.AgendarAsync` |
| RN-022/RN-025 | Desativação de veterinário encerra o acesso e retorna agendamentos futuros ao chamador | `VeterinarioService.DesativarAsync` |
| RN-026 | Endereço do prestador persistido no próprio registro, com latitude/longitude derivadas dele — nunca informadas pelo cliente | `Endereco` (owned type em TB_VETERINARIO) |
| RN-033/RN-057 | Nota só é pública a partir de 3 avaliações; `PUBLICADO_EM` ancora o selo "Novo na Vetly" por 30 dias | `Veterinario.TemNotaPublica` + `PublicarNoMatching` |
| RN-039 | Atendimento remoto está fora do escopo desta fase — agendamento aceita apenas `Presencial` | `ConsultaService.AgendarAsync` + `AtualizarAsync` |
| RN-041 | Cancelamento com mais de 24h de antecedência = reembolso integral | `ReembolsoIntegralStrategy` |
| RN-041/RN-042 | Cancelamento entre 2h e 24h = reembolso parcial (retenção de 30% — ainda fixa, ver C-06) | `ReembolsoParcialStrategy` |
| RN-041 | Cancelamento com menos de 2h = sem reembolso | `SemReembolsoStrategy` |
| RN-081 | Sugestão de dose exige peso do animal — `POST /api/ia/protocolo` com peso ausente/zero devolve 422, e o cadastro do pet passa a exigir `pesoKg` | `OllamaService.SugerirProtocoloAsync` + `AnimalService` |
| RN-082 | Documentos só podem ser gerados após `consulta.DiagnosticoValidado = true` E pagamento confirmado | `DocumentoService.GerarAsync` |
| RN-087 | Finalizar consulta exige documento `ReceitaVeterinaria` assinado digitalmente | `ConsultaService.FinalizarAsync` |
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
