# Vetly API

API RESTful para gestao de clinicas veterinarias com assistente de IA (Ollama).

---

## 1. Visao Geral

O **Vetly** e uma plataforma de gestao veterinaria que cobre o ciclo completo de atendimento: agendamento de consultas, prontuarios eletronicos, internacoes, exames laboratoriais, emissao de documentos clinicos, pagamentos com split financeiro e triagem inteligente via LLM local (Ollama/llama3.2).

---

## 2. Arquitetura

```
┌─────────────────────────────────────────────────────┐
│                  Vetly.API (.NET 10)                 │
│  Controllers · Middlewares · Program.cs · JWT        │
└────────────────────┬────────────────────────────────┘
                     │ depende de
┌────────────────────▼────────────────────────────────┐
│               Vetly.Application                      │
│  Services · Interfaces · DTOs · Factories · Strategy │
└─────────┬──────────────────────────┬────────────────┘
          │ depende de               │ depende de
┌─────────▼──────────┐  ┌────────────▼───────────────┐
│   Vetly.Domain     │  │   Vetly.Infrastructure      │
│  Entities · Enums  │  │  EF Core · Oracle · Repos   │
│  Value Objects     │  │                             │
└────────────────────┘  └─────────────────────────────┘

Tests:
  Vetly.UnitTests         → Application, Domain
  Vetly.IntegrationTests  → API
```

---

## 3. Stack Tecnologica

| Componente | Tecnologia |
|---|---|
| Framework | ASP.NET Core 10 (Web API) |
| ORM | Entity Framework Core 10 + Oracle.EntityFrameworkCore |
| Banco de Dados | Oracle Database 21c+ |
| Autenticacao | JWT Bearer (Microsoft.AspNetCore.Authentication.JwtBearer) |
| Documentacao | Scalar / OpenAPI (tema DeepSpace) |
| IA Assistente | Ollama local (llama3.2) via HttpClient |
| Testes | xUnit + Moq |

---

## 4. Padroes e Principios

| Padrao | Onde |
|---|---|
| **Factory Pattern** | `DocumentoService` seleciona `IDocumentoFactory` pelo `TipoDocumento` |
| **Strategy Pattern** | `ConsultaService` seleciona `ICancelamentoStrategy` por prioridade e antecedencia |
| **Strategy Pattern** | `PagamentoService` seleciona `ISplitFinanceiroStrategy` pela `PersonaVeterinario` |
| **Repository Pattern** | Interfaces em Application; implementacoes EF Core em Infrastructure |
| **Dependency Inversion** | Todos os servicos dependem de interfaces — zero acoplamento concreto |
| **Soft Delete** | Veterinario, Animal e Tutor sao desativados (nunca deletados) |
| **Value Object** | `Crmv` — imutavel, valida regex `^\d{4,6}-[A-Z]{2}$` |
| **ProblemDetails** | `ExceptionHandlingMiddleware` retorna RFC 7807 em todos os erros |

---

## 5. Pre-requisitos

- .NET 10 SDK
- Oracle Database 21c+ (ou Oracle XE)
- Ollama rodando localmente (`ollama serve` + `ollama pull llama3.2`)
- Docker (opcional, para Oracle)

---

## 6. Configuracao

### appsettings.json

```json
{
  "ConnectionStrings": {
    "OracleConnection": "User Id=vetly;Password=senha;Data Source=localhost:1521/VETLYDB"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama3.2",
    "TimeoutSeconds": 120
  },
  "Jwt": {
    "Key": "SuaChaveSecretaComMinimo32Caracteres!",
    "Issuer": "Vetly",
    "Audience": "VetlyAPI"
  }
}
```

### Variaveis de Ambiente (producao)

```bash
ConnectionStrings__OracleConnection="..."
Jwt__Key="..."
```

---

## 7. Banco de Dados (Oracle)

### Migration e Update

```bash
# Criar a migration inicial
dotnet ef migrations add InitialCreate \
  --project src/Vetly.Infrastructure \
  --startup-project src/Vetly.API

# Aplicar ao banco
dotnet ef database update \
  --project src/Vetly.Infrastructure \
  --startup-project src/Vetly.API
```

### Convencoes Oracle

| Tipo C# | Coluna Oracle |
|---|---|
| `string` | `VARCHAR2(N)` |
| `bool` | `NUMBER(1)` |
| `decimal` | `NUMBER(18,2)` |
| `Guid` (PK) | `CHAR(36)` |

### Tabelas

`TB_VETERINARIO`, `TB_ANIMAL`, `TB_TUTOR`, `TB_CONSULTA`, `TB_PRONTUARIO`, `TB_EXAME`, `TB_INTERNACAO`, `TB_DOCUMENTO`, `TB_PAGAMENTO`, `TB_EMPRESA`

---

## 8. Ollama (IA Assistente)

```bash
# Instalar e iniciar o Ollama
ollama serve
ollama pull llama3.2

# Testar diretamente
curl -X POST http://localhost:11434/api/generate \
  -H "Content-Type: application/json" \
  -d '{"model":"llama3.2","prompt":"Ola!","stream":false}'
```

> **Importante (RN-024):** todos os endpoints `/api/ia/*` retornam apenas *sugestoes* do modelo. O veterinario deve validar diagnosticos e protocolos manualmente antes de gerar documentos ou iniciar tratamentos.

---

## 9. Rotas da API

### Veterinarios
| Metodo | Rota | Descricao |
|---|---|---|
| GET | `/api/veterinarios` | Lista todos ativos |
| GET | `/api/veterinarios/{id}` | Detalhe |
| GET | `/api/veterinarios/regiao/{uf}` | Por UF |
| GET | `/api/veterinarios/{id}/agenda` | Agenda futura |
| POST | `/api/veterinarios` | Cadastrar (RN-011) |
| PUT | `/api/veterinarios/{id}` | Atualizar |
| DELETE | `/api/veterinarios/{id}` | Desativar, retorna agendamentos (RN-008) |

### Animais
| Metodo | Rota | Descricao |
|---|---|---|
| GET | `/api/animais` | Lista todos ativos |
| GET | `/api/animais/{id}` | Detalhe |
| GET | `/api/animais/{id}/prontuarios` | Historico longitudinal |
| GET | `/api/animais/{id}/exames` | Exames do animal |
| POST | `/api/animais` | Cadastrar |
| PUT | `/api/animais/{id}` | Atualizar |
| DELETE | `/api/animais/{id}` | Desativar (soft delete) |

### Tutores
| Metodo | Rota | Descricao |
|---|---|---|
| GET | `/api/tutores` | Lista todos ativos |
| GET | `/api/tutores/{id}` | Detalhe |
| GET | `/api/tutores/{id}/animais` | Animais do tutor |
| POST | `/api/tutores` | Cadastrar |
| PUT | `/api/tutores/{id}` | Atualizar |
| DELETE | `/api/tutores/{id}` | Desativar (soft delete + anonimizacao LGPD) |

### Consultas
| Metodo | Rota | Descricao |
|---|---|---|
| GET | `/api/consultas` | Filtros: dataInicio, dataFim, veterinarioId, cancelada |
| GET | `/api/consultas/veterinario/{id}` | Por veterinario |
| GET | `/api/consultas/animal/{id}` | Por animal |
| GET | `/api/consultas/{id}/briefing` | Briefing pre-consulta: animal, historico e exames |
| POST | `/api/consultas` | Agendar (RN-015: pagamento confirmado) |
| POST | `/api/consultas/{id}/finalizar` | Finalizar — exige receita assinada (RN-031) |
| DELETE | `/api/consultas/{id}` | Cancelar + Strategy reembolso (RN-019/020/021) |

### Internacoes
| Metodo | Rota | Descricao |
|---|---|---|
| POST | `/api/internacoes` | Abrir internacao |
| PUT | `/api/internacoes/{id}/procedimentos` | Registrar procedimentos do dia |
| POST | `/api/internacoes/{id}/alta` | Dar alta — retorna saldo restante (RN-016) |

### Lembretes
| Metodo | Rota | Descricao |
|---|---|---|
| POST | `/api/lembretes` | Agendar lembrete (vacina, retorno, medicacao…) |
| POST | `/api/lembretes/{id}/tentativa` | Registrar tentativa de contato (alerta apos 3 — RN-029) |
| POST | `/api/lembretes/{id}/resposta` | Registrar resposta do tutor — encerra regua (RN-030) |

### Exames
| Metodo | Rota | Descricao |
|---|---|---|
| POST | `/api/exames` | Solicitar exame |
| PUT | `/api/exames/{id}/resultado` | Registrar resultado |
| PUT | `/api/exames/{id}/liberar` | Liberar ao tutor |

### Documentos
| Metodo | Rota | Descricao |
|---|---|---|
| POST | `/api/documentos/consulta/{id}?tipo=Prontuario` | Gerar documento (RN-024) |
| POST | `/api/documentos/{id}/assinar` | Assinar digitalmente (RN-031) |
| POST | `/api/documentos/{id}/correcao` | Criar versao corrigida (RN-032/033/034) |

### Pagamentos
| Metodo | Rota | Descricao |
|---|---|---|
| GET | `/api/pagamentos` | Lista todos os pagamentos |
| GET | `/api/pagamentos/{id}` | Detalhe |
| POST | `/api/pagamentos` | Registrar pagamento |
| POST | `/api/pagamentos/{id}/processar-split` | Split financeiro via Strategy |

### Empresas
| Metodo | Rota | Descricao |
|---|---|---|
| GET | `/api/empresas` | Lista todas ativas |
| GET | `/api/empresas/{id}` | Detalhe |
| GET | `/api/empresas/{id}/veterinarios` | Veterinarios da empresa |
| POST | `/api/empresas` | Cadastrar |
| POST | `/api/empresas/{id}/veterinarios/{vetId}` | Vincular veterinario |
| PUT | `/api/empresas/{id}` | Atualizar |
| DELETE | `/api/empresas/{id}` | Desativar |

### IA (Ollama)
| Metodo | Rota | Descricao |
|---|---|---|
| POST | `/api/ia/diagnostico` | Sugerir hipoteses diagnosticas |
| POST | `/api/ia/protocolo` | Sugerir protocolo de tratamento |
| POST | `/api/ia/triagem` | Triar sintomas por urgencia |
| POST | `/api/ia/orientacoes` | Orientacoes pos-atendimento para o tutor |

---

## 10. Exemplos curl

```bash
# 1. Gerar token JWT (endpoint publico — sem autenticacao previa)
curl -X POST https://localhost:7262/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"usuario": "admin-teste", "role": "Admin"}'
# Retorna: { "token": "eyJ...", "role": "Admin", "expiraEm": "..." }

# 2. Cadastrar veterinario (RN-011: CRMV validado)
curl -X POST https://localhost:7262/api/veterinarios \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "nome": "Dra. Ana Lima",
    "crmv": "12345-SP",
    "ufAtuacao": "SP",
    "persona": 0,
    "plano": 1,
    "especialidades": ["Clinica Geral"],
    "especiesAtendidas": ["Canino", "Felino"]
  }'

# 3. Agendar consulta (RN-015: pagamento deve estar confirmado)
curl -X POST https://localhost:7262/api/consultas \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "dataHora": "2026-06-01T14:00:00Z",
    "modalidade": 0,
    "veterinarioId": "{guid}",
    "animalId": "{guid}",
    "tutorId": "{guid}",
    "pagamentoId": "{guid}"
  }'

# 4. Sugerir diagnostico via IA
curl -X POST https://localhost:7262/api/ia/diagnostico \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "especie": "Canino",
    "raca": "Labrador",
    "idadeAnos": 5,
    "pesoKg": 30,
    "sintomas": ["vomito", "apatia", "perda de apetite"]
  }'
```

---

## 11. Regras de Negocio

| RN | Descricao | Implementacao |
|---|---|---|
| RN-001/007 | Controle de acesso por perfil (Admin, Veterinario) | Policies JWT + `[Authorize(Policy)]` |
| RN-008 | Desativacao de vet retorna agendamentos futuros | `VeterinarioService.DesativarAsync` |
| RN-011 | CRMV validado por regex + mock CFMV | `VeterinarioService.ValidarCrmv` |
| RN-015 | Consulta exige pagamento confirmado | `ConsultaService.AgendarAsync` |
| RN-016 | Alta de internacao calcula saldo restante (caucao - total apurado) | `InternacaoService.DarAltaAsync` → `AltaInternacaoDto` |
| RN-019 | Cancelamento >24h = reembolso integral | `ReembolsoIntegralStrategy` |
| RN-020 | Cancelamento 2-24h = reembolso parcial | `ReembolsoParcialStrategy` |
| RN-021 | Cancelamento <2h = sem reembolso | `SemReembolsoStrategy` |
| RN-024 | IA so sugere, veterinario valida | `DocumentoService.GerarAsync` |
| RN-029 | Apos 3 tentativas sem resposta, alerta e enviado a clinica | `LembreteService.ProcessarTentativaAsync` |
| RN-030 | Resposta do tutor encerra regua de contato | `LembreteService.RegistrarRespostaAsync` |
| RN-031 | Finalizacao de consulta exige receita assinada digitalmente | `ConsultaService.FinalizarAsync` |
| RN-032/033 | Correcao cria nova versao do documento (original preservado) | `DocumentoService.CorrigirAsync` |
| RN-034 | Correcao apos 24h exige justificativa | `DocumentoService.CorrigirAsync` |

---

## 11.1 Discrepancia Corrigida (CRMV invalido → 400)

**Problema:** `POST /api/veterinarios` com CRMV no formato invalido retornava 422 em vez de 400.

**Causa raiz:** `VeterinarioRepository.ObterPorCrmvAsync` usava `EF.Property<string>(v, "CRMV")` para acessar a shadow property do value object `Crmv`. No InMemory (usado nos testes), essa shadow property nao existe com esse nome — o EF Core InMemory armazena por caminho de navegacao. Resultado: `InvalidOperationException` → middleware → 422.

**Solucao:**
1. `ObterPorCrmvAsync`: substituido `EF.Property<string>(v, "CRMV")` por `v.Crmv.Valor` — funciona com Oracle (translata para coluna "CRMV") e InMemory (avalia em memoria).
2. `VeterinarioConfiguration`: removido `HasIndex("CRMV")` que tambem falhava em InMemory. Unicidade de CRMV e garantida na camada de aplicacao (`ValidarCrmv`).
3. `WebApplicationFactory`: adicionado `UseInternalServiceProvider` com provider InMemory isolado para evitar conflito dual-provider Oracle + InMemory.

---

## 12. Commits Convencionais

O historico segue [Conventional Commits](https://www.conventionalcommits.org/) em pt-BR:

```
chore(solucao): inicializa solucao .NET
feat(domain): adiciona entidades de dominio
feat(infra): configura VetlyDbContext e repositorios Oracle
feat(application): implementa Factory e Strategy patterns
feat(application): implementa servicos e interfaces
refactor(application): move interfaces de repositorio (DIP)
feat(api): implementa controllers, middlewares e Program.cs
test(unitarios): adiciona testes das strategies e servicos
docs(readme): adiciona README completo
```

---

## 13. Fases de Desenvolvimento

| Fase | Descricao | Status |
|---|---|---|
| 1 | Solution e projetos .NET com referencias | ✅ Concluida |
| 2 | Domain — enums, value objects e entidades | ✅ Concluida |
| 3 | Infrastructure — DbContext, Oracle, repositorios | ✅ Concluida |
| 4 | Application — Factory e Strategy patterns | ✅ Concluida |
| 5 | Application — DTOs, interfaces e servicos | ✅ Concluida |
| 6 | API — Controllers, middlewares, JWT, Scalar | ✅ Concluida |
| 7 | Testes unitarios e de integracao (47 testes, 100% aprovados) | ✅ Concluida |
| 8 | Correcao CRMV 400 + RN-016/029/030/031/032/034 + Lembretes + Auth + Briefing | ✅ Concluida |
| 9 | README completo | ✅ Concluida |

---

## 14. Cobertura de Testes

| Suite | Quantidade | O que cobre |
|---|---|---|
| `Vetly.UnitTests` | 40 testes | CancelamentoStrategies, DocumentoService, OllamaService, ConsultaService, PagamentoService, VeterinarioService, InternacaoService, LembreteService |
| `Vetly.IntegrationTests` | 7 testes | Autenticacao JWT (401/403), validacao CRMV (400), duplicata CRMV (422), IA endpoint |
| **Total** | **47 testes** | **100% aprovados** |

---

## Executar Localmente

```bash
# Restaurar dependencias e compilar
dotnet restore
dotnet build

# Aplicar migrations no banco Oracle
dotnet ef database update \
  --project src/Vetly.Infrastructure \
  --startup-project src/Vetly.API

# Executar a API (perfil HTTPS)
dotnet run --project src/Vetly.API --launch-profile https

# Acessar documentacao Scalar
# https://localhost:7262/scalar/v1

# Executar testes
dotnet test
```
