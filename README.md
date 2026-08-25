# Vetly API — Detalhamento do Projeto

O Vetly é uma API REST para gestão de clínicas veterinárias, cobrindo todo o ciclo do atendimento — agendamento de consultas com pagamento integrado, prontuários, internações com apuração financeira, exames, emissão de documentos clínicos (prontuário, receita, atestado, nota fiscal) e split financeiro entre profissionais e empresas. Integra um assistente de IA local (Ollama) para sugerir hipóteses diagnósticas, protocolos de tratamento, triagem de sintomas e orientações pós-atendimento, sempre validados manualmente pelo veterinário (RN-024).

## Stack

| Camada | Tecnologia |
|---|---|
| Framework | ASP.NET Core 10 (Web API) |
| ORM | EF Core 10 + Oracle.EntityFrameworkCore |
| Banco | Oracle Database 21c+ |
| Autenticação | JWT Bearer |
| Documentação | Scalar (tema DeepSpace) em `/scalar/v1` |
| IA | Ollama local (modelo `llama3.1`) |
| Testes | xUnit + Moq (51 testes verdes) |

## Padrões aplicados

| Padrão | Onde |
|---|---|
| Factory Pattern | `DocumentoService` seleciona `IDocumentoFactory` pelo `TipoDocumento` (Prontuario, Receita, Atestado, NotaFiscal) |
| Strategy Pattern | `ConsultaService` seleciona `ICancelamentoStrategy` por antecedência (RN-019/020/021) |
| Strategy Pattern | `PagamentoService` seleciona `ISplitFinanceiroStrategy` pela `PersonaVeterinario` (autônomo vs. vinculado) |
| Repository Pattern | Interfaces em `Vetly.Application`; implementações EF Core em `Vetly.Infrastructure` |
| DIP | Todos os serviços dependem de interfaces — zero acoplamento concreto |
| Soft Delete | `Veterinario`, `Animal` e `Tutor` são desativados, nunca deletados |
| Value Object | `Crmv` — imutável, valida regex `^\d{4,6}-[A-Z]{2}$` |
| ProblemDetails | `ExceptionHandlingMiddleware` retorna RFC 7807 em todos os erros |

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
| POST | `/api/veterinarios` | Cadastrar — requer role Admin (RN-011) |
| PUT | `/api/veterinarios/{id}` | Atualizar |
| DELETE | `/api/veterinarios/{id}` | Desativar — requer role Admin, retorna agendamentos futuros (RN-008) |

### Animais
| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/animais` | Lista todos ativos |
| GET | `/api/animais/{id}` | Detalhe |
| GET | `/api/animais/{id}/prontuarios` | Histórico longitudinal de prontuários |
| GET | `/api/animais/{id}/exames` | Exames do animal |
| POST | `/api/animais` | Cadastrar |
| PUT | `/api/animais/{id}` | Atualizar |
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
| POST | `/api/consultas` | Agendar — requer pagamento confirmado (RN-015) |
| PUT | `/api/consultas/{id}/validar-diagnostico` | Registra validação manual do diagnóstico (RN-024) |
| POST | `/api/consultas/{id}/finalizar` | Finalizar — exige receita assinada (RN-031) |
| DELETE | `/api/consultas/{id}` | Cancelar + Strategy de reembolso (RN-019/020/021) |

### Internações
| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/internacoes` | Lista todas |
| GET | `/api/internacoes/{id}` | Detalhe |
| POST | `/api/internacoes` | Abrir internação |
| PUT | `/api/internacoes/{id}/procedimentos` | Registrar procedimentos do dia e acumular valor apurado (RN-016) |
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
| POST | `/api/documentos/consulta/{id}?tipo={TipoDocumento}` | Gerar via Factory — exige diagnóstico validado (RN-024) |
| POST | `/api/documentos/{id}/assinar` | Assinar digitalmente (RN-031) |
| POST | `/api/documentos/{id}/correcao` | Criar versão corrigida — após 24h exige justificativa (RN-032/034) |

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
| POST | `/api/lembretes/{id}/tentativa` | Registrar tentativa de contato — após 3 sem resposta, alerta à clínica (RN-029) |
| POST | `/api/lembretes/{id}/resposta` | Registrar resposta do tutor — encerra régua (RN-030) |

### IA (Ollama)
| Método | Rota | Descrição |
|---|---|---|
| POST | `/api/ia/diagnostico` | Sugerir hipóteses diagnósticas |
| POST | `/api/ia/protocolo` | Sugerir protocolo de tratamento |
| POST | `/api/ia/triagem` | Triar sintomas por urgência |
| POST | `/api/ia/orientacoes` | Orientações pós-atendimento para o tutor |

> **Todas as respostas da IA são sugestões — o veterinário deve validar manualmente antes de gerar qualquer documento clínico (RN-024).**

---

## Regras de Negócio

| Código | Descrição | Implementação |
|---|---|---|
| RN-008 | Desativação de veterinário retorna agendamentos futuros ao chamador | `VeterinarioService.DesativarAsync` |
| RN-011 | CRMV validado por regex `^\d{4,6}-[A-Z]{2}$` e verificação de duplicidade | `VeterinarioService.CriarAsync` |
| RN-015 | Consulta só pode ser agendada se o pagamento estiver com status Confirmado | `ConsultaService.AgendarAsync` |
| RN-016 | Procedimentos acumulam `ValorTotalApurado`; alta retorna `saldo = total − caução` | `InternacaoService.RegistrarProcedimentosAsync` + `DarAltaAsync` |
| RN-019 | Cancelamento com mais de 24h de antecedência = reembolso integral | `ReembolsoIntegralStrategy` |
| RN-020 | Cancelamento entre 2h e 24h = reembolso parcial (70% devolvido) | `ReembolsoParcialStrategy` |
| RN-021 | Cancelamento com menos de 2h = sem reembolso | `SemReembolsoStrategy` |
| RN-024 | Documentos só podem ser gerados após `consulta.DiagnosticoValidado = true` E pagamento confirmado | `DocumentoService.GerarAsync` |
| RN-029 | Após 3 tentativas sem resposta, `AlertaEnviadoClinica = true` | `LembreteService.ProcessarTentativaAsync` |
| RN-030 | Resposta do tutor encerra a régua de contato | `LembreteService.RegistrarRespostaAsync` |
| RN-031 | Finalizar consulta exige documento `ReceitaVeterinaria` assinado digitalmente | `ConsultaService.FinalizarAsync` |
| RN-032/033 | Correção cria nova versão do documento (original preservado com `VersaoOriginalId`) | `DocumentoService.CorrigirAsync` |
| RN-034 | Correção após 24h exige justificativa não vazia | `DocumentoService.CorrigirAsync` |
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
