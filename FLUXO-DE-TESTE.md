# Fluxo de Teste Ponta a Ponta — Vetly API v2

Roteiro para testar a API inteira via `curl`, do cadastro ao ciclo completo de uma
consulta: agendamento → pagamento simulado → IA assistida → documento assinado →
avaliação → fidelidade → dashboard financeiro do Admin. Cada bloco reaproveita o `id`
retornado pelo bloco anterior — copie os valores conforme for testando, ou rode tudo
de uma vez com `jq` (instruções no fim de cada bloco).

Alternativa visual: suba a API e abra `https://localhost:7262/scalar/v1` — todos os
endpoints abaixo estão documentados lá, com schemas de request/response completos.

> Pré-requisito: siga a seção "Instalação e como rodar" do [`README.md`](./README.md)
> antes de começar — API rodando em `https://localhost:7262`, Ollama rodando em
> `http://localhost:11434` com o modelo `llama3.1` baixado.

Convenção usada abaixo: `export` guarda cada id numa variável de shell para o próximo
comando reaproveitar. Se preferir, rode um bloco por vez e cole os valores manualmente.

```bash
BASE=https://localhost:7262
# se seu curl reclamar de certificado local (dev), adicione -k em todas as chamadas
```

---

## Passo 1 — Autenticação

Gere três tokens: um Admin (vai administrar a empresa), um Veterinário e um
Responsável. No mundo real cada ator logaria separadamente — aqui geramos os três de
uma vez para ter à mão.

```bash
TOKEN_ADMIN=$(curl -s -X POST $BASE/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"usuario":"admin-clinica","role":"Admin"}' | jq -r .token)

echo "Admin: $TOKEN_ADMIN"
```

> Repare que ainda não passamos `entidadeId` — vamos gerar o token final do Admin e do
> Vet só depois de cadastrá-los (Passos 2 e 3), para vincular o token a um registro
> real e poder testar as checagens de posse (RN-001..007).

**Checklist:** `200 OK`, corpo `{ token, role: "Admin", expiraEm }`.

---

## Passo 2 — Cadastrar a Empresa

```bash
EMPRESA_ID=$(curl -s -X POST $BASE/api/empresas \
  -H "Authorization: Bearer $TOKEN_ADMIN" \
  -H "Content-Type: application/json" \
  -d '{
    "nome": "Clínica Pet Saúde",
    "tipo": "Clinica",
    "administradorId": "'$(uuidgen)'"
  }' | jq -r .id)

echo "Empresa: $EMPRESA_ID"
```

> Sem `uuidgen`? Use qualquer GUID fixo, ex. `11111111-1111-1111-1111-111111111111`
> — `administradorId` no MVP é só um `Guid` solto (não existe entidade `Administrador`
> separada, ver `AGENT-OBJECTIVES.md`).

**Checklist:** `201 Created`, `Location` aponta para `GET /api/empresas/{id}`. Note
`faixaEnterprise: 0` — ainda não tem vet vinculado.

**Regenere o token do Admin agora, vinculado à empresa** (necessário para o dashboard
no Passo 14):

```bash
TOKEN_ADMIN=$(curl -s -X POST $BASE/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"usuario":"admin-clinica","role":"Admin","entidadeId":"'$EMPRESA_ID'"}' | jq -r .token)
```

---

## Passo 3 — Cadastrar o Veterinário e vincular à empresa

```bash
VET_ID=$(curl -s -X POST $BASE/api/veterinarios \
  -H "Authorization: Bearer $TOKEN_ADMIN" \
  -H "Content-Type: application/json" \
  -d '{
    "nome": "Dra. Ana Souza",
    "crmv": "12345-SP",
    "ufAtuacao": "SP",
    "persona": "Vinculado",
    "plano": "Profissional",
    "especialidades": ["Clínica Geral"],
    "especiesAtendidas": ["Canino", "Felino"]
  }' | jq -r .id)

echo "Veterinario: $VET_ID"

curl -s -X POST $BASE/api/empresas/$EMPRESA_ID/veterinarios/$VET_ID \
  -H "Authorization: Bearer $TOKEN_ADMIN" -o /dev/null -w "vincular vet: %{http_code}\n"
```

**Checklist:** cadastro `201 Created` (RN-011 valida o CRMV `12345-SP`); vínculo
`204 No Content`. Confira a faixa Enterprise:

```bash
curl -s $BASE/api/empresas/$EMPRESA_ID/assinatura \
  -H "Authorization: Bearer $TOKEN_ADMIN" | jq
# esperado: qtdVeterinariosAtivos: 1, faixaEnterprise: 599 (RN-092, faixa 1-5)
```

**Gere o token do vet, vinculado ao registro** (necessário para assinar documentos e
marcar consultas como realizadas — RN-031, RN-001..006):

```bash
TOKEN_VET=$(curl -s -X POST $BASE/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"usuario":"dra-ana","role":"Veterinario","entidadeId":"'$VET_ID'"}' | jq -r .token)
```

---

## Passo 4 — Cadastrar o Responsável

```bash
RESP_ID=$(curl -s -X POST $BASE/api/responsaveis \
  -H "Authorization: Bearer $TOKEN_ADMIN" \
  -H "Content-Type: application/json" \
  -d '{
    "nome": "Carlos Pereira",
    "email": "carlos.pereira@teste.com",
    "telefone": "11988887777"
  }' | jq -r .id)

echo "Responsavel: $RESP_ID"
```

**Checklist:** `201 Created`. `GET /api/responsaveis/{id}` deve mostrar
`tierFidelidade: "Bronze"`, `saldoPontos: 0`.

---

## Passo 5 — Consentimento LGPD (obrigatório para agendar)

Sem consentimento `AtendimentoClinico` ativo, agendar consulta falha com `LGPD-001`
(teste isso deliberadamente **antes** de conceder — ver bloco "erro esperado" abaixo).

```bash
# Erro esperado (sem consentimento ainda):
curl -s -X POST $BASE/api/consultas \
  -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" \
  -d '{"dataHora":"2026-06-01T14:00:00Z","modalidade":"Presencial","tipoServico":"Consulta","veterinarioId":"'$VET_ID'","animalId":"'$(uuidgen)'","responsavelId":"'$RESP_ID'"}' \
  -w "\nstatus: %{http_code}\n"
# esperado: 422, codigo "LGPD-001"

curl -s -X POST $BASE/api/responsaveis/$RESP_ID/consentimentos \
  -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" \
  -d '{"finalidade": "AtendimentoClinico"}' -o /dev/null -w "consentimento clinico: %{http_code}\n"

# Conceda também CompartilhamentoRede — necessário para a colmeia por evento clínico (RN-083, Passo 12)
curl -s -X POST $BASE/api/responsaveis/$RESP_ID/consentimentos \
  -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" \
  -d '{"finalidade": "CompartilhamentoRede"}' -o /dev/null -w "consentimento rede: %{http_code}\n"
```

**Checklist:** primeira chamada `422 LGPD-001`; as duas concessões `201 Created`.
`GET /api/responsaveis/{id}/consentimentos` deve listar as duas, `ativo: true`.

---

## Passo 6 — Cadastrar o Animal e gerar o calendário de obrigações

```bash
ANIMAL_ID=$(curl -s -X POST $BASE/api/animais \
  -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" \
  -d '{
    "nome": "Rex",
    "especie": "Canino",
    "raca": "Labrador",
    "sexo": "Macho",
    "dataNascimento": "2022-03-15T00:00:00Z",
    "responsavelId": "'$RESP_ID'",
    "pesoKg": 28.5
  }' | jq -r .id)

echo "Animal: $ANIMAL_ID"

curl -s -X POST $BASE/api/animais/$ANIMAL_ID/obrigacoes \
  -H "Authorization: Bearer $TOKEN_ADMIN" | jq
```

**Checklist:** animal `201 Created`. Calendário `201 Created` retorna 4 obrigações
(`Vacina`, `Vermifugo`, `CheckUp`, `Retorno` — factory canina, RN-069), todas
`status: "Pendente"`. Tentar gerar de novo deve falhar com `422 OBRIGACAO-002`.

---

## Passo 7 — Agendar a consulta (agora com consentimento ativo)

```bash
CONSULTA_ID=$(curl -s -X POST $BASE/api/consultas \
  -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" \
  -d '{
    "dataHora": "2026-06-01T14:00:00Z",
    "modalidade": "Presencial",
    "tipoServico": "Consulta",
    "veterinarioId": "'$VET_ID'",
    "animalId": "'$ANIMAL_ID'",
    "responsavelId": "'$RESP_ID'",
    "preSintomas": "Vômito há 2 dias, apetite reduzido"
  }' | jq -r .id)

echo "Consulta: $CONSULTA_ID"
```

**Checklist:** `201 Created`, `status: "EmCheckout"`, `lockCheckoutExpiraEm` preenchido
(10 minutos à frente — RN-058). Se demorar mais de 10 min até o próximo passo, o lock
expira e a confirmação de pagamento falha com `CONSULTA-011` — reagende se precisar.

---

## Passo 8 — Simular o pagamento (confirma a consulta)

```bash
curl -s -X POST $BASE/api/pagamentos/simular \
  -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" \
  -d '{"consultaId":"'$CONSULTA_ID'","valor":150.00,"meio":"Pix"}' | jq
```

**Checklist:** `201 Created`, `status: "Confirmado"`, `simulado: true`,
`percentualComissao: 12` (plano Profissional — RN-089), `valorComissao: 18.00`,
`valorRepasse: 132.00`, `descontoFidelidadeCalculado: 0` (tier Bronze ainda —
RN-072), `consultaStatus: "Confirmada"`.

```bash
curl -s $BASE/api/consultas/$CONSULTA_ID \
  -H "Authorization: Bearer $TOKEN_ADMIN" | jq '.status, .lockCheckoutExpiraEm'
# esperado: "Confirmada"
```

---

## Passo 9 — Briefing pré-consulta (como o vet autenticado)

```bash
curl -s $BASE/api/consultas/$CONSULTA_ID/briefing \
  -H "Authorization: Bearer $TOKEN_VET" | jq '.animal.nome, .preSintomas, .alertasAtivos'
```

**Checklist:** `200 OK` — o acesso é concedido porque a consulta confirmada com
`CompartilhamentoRede` ativo já gerou uma concessão de colmeia (RN-083). Confira o log:

```bash
curl -s $BASE/api/animais/$ANIMAL_ID/log-acessos \
  -H "Authorization: Bearer $TOKEN_ADMIN" | jq
# esperado: 1 registro, contexto "Briefing pré-consulta ..."
```

---

## Passo 10 — IA da consulta: diagnóstico, protocolo e decisão

```bash
LOG_DIAG=$(curl -s -X POST $BASE/api/consultas/$CONSULTA_ID/ia/diagnostico \
  -H "Authorization: Bearer $TOKEN_VET" | tee /tmp/diag.json | jq -r .logId)
jq '.hipoteses' /tmp/diag.json

curl -s -X POST $BASE/api/consultas/$CONSULTA_ID/ia/decisao \
  -H "Authorization: Bearer $TOKEN_VET" -H "Content-Type: application/json" \
  -d '{"tipo":"Diagnostico","decisao":"Aprovar"}' | jq

LOG_PROT=$(curl -s -X POST $BASE/api/consultas/$CONSULTA_ID/ia/protocolo \
  -H "Authorization: Bearer $TOKEN_VET" | tee /tmp/prot.json | jq -r .logId)
jq '.medicamentos, .alertasInteracao' /tmp/prot.json

curl -s -X POST $BASE/api/consultas/$CONSULTA_ID/ia/decisao \
  -H "Authorization: Bearer $TOKEN_VET" -H "Content-Type: application/json" \
  -d '{"tipo":"Protocolo","decisao":"Aprovar"}' | jq
```

**Checklist:** cada sugestão `200 OK` com `logId`; cada decisão `200 OK` com
`estadoFinalDefinido: true` na segunda (protocolo, já que diagnóstico é o gate
principal — RN-099). Auditoria completa:

```bash
curl -s $BASE/api/consultas/$CONSULTA_ID/ia/auditoria \
  -H "Authorization: Bearer $TOKEN_VET" | jq 'length'
# esperado: 2 (diagnostico + protocolo, ambos com decisao "Aprovar", pendente: false)
```

---

## Passo 11 — Gerar e assinar a receita

```bash
DOC_ID=$(curl -s -X POST "$BASE/api/documentos/consulta/$CONSULTA_ID?tipo=ReceitaVeterinaria" \
  -H "Authorization: Bearer $TOKEN_VET" | jq -r .id)

echo "Documento: $DOC_ID"

curl -s -X POST $BASE/api/documentos/$DOC_ID/assinar \
  -H "Authorization: Bearer $TOKEN_VET" -H "Content-Type: application/json" \
  -d '{"nomeDigitado":"Dra. Ana Souza"}' -w "\nstatus: %{http_code}\n"
```

**Checklist:** geração `201 Created` (exige estado final — já garantido no Passo 10,
senão seria `422 CONSULTA-012`). Assinatura `204 No Content` — o nome bate com o vet
autenticado. Teste o erro deliberadamente com um nome errado antes de assinar de
verdade, se quiser ver `422 DOCUMENTO-002`.

```bash
curl -s $BASE/api/documentos/$DOC_ID \
  -H "Authorization: Bearer $TOKEN_VET" | jq '.assinadoDigitalmente, .habilitaDispensacaoControlados'
# esperado: true, false (RN-091 — nome digitado nunca habilita dispensação de controlados)
```

---

## Passo 12 — Marcar a consulta como realizada (dispara fidelidade)

```bash
curl -s -X POST $BASE/api/consultas/$CONSULTA_ID/realizada \
  -H "Authorization: Bearer $TOKEN_VET" -w "\nstatus: %{http_code}\n" | jq
```

**Checklist:** `200 OK`, `status: "Realizada"`, `dataRealizada` preenchida. Se
tentasse marcar sem a receita assinada, seria `422 RN-031`; se outro vet tentasse,
`403 ACESSO-002`.

```bash
curl -s $BASE/api/responsaveis/$RESP_ID/fidelidade \
  -H "Authorization: Bearer $TOKEN_ADMIN" | jq
# esperado: saldoPontos: 50 (cumpriu a obrigação CheckUp pendente no prazo — RN-070), tierFidelidade "Bronze"

curl -s $BASE/api/responsaveis/$RESP_ID/fidelidade/extrato \
  -H "Authorization: Bearer $TOKEN_ADMIN" | jq
# esperado: 1 lançamento, origem "ObrigacaoCumprida", pontos 50, valido true

curl -s $BASE/api/animais/$ANIMAL_ID/obrigacoes \
  -H "Authorization: Bearer $TOKEN_ADMIN" | jq '.[] | select(.tipo=="CheckUp")'
# esperado: status "Cumprida", consultaId preenchido
```

---

## Passo 13 — Avaliar a consulta

```bash
AVALIACAO_ID=$(curl -s -X POST $BASE/api/consultas/$CONSULTA_ID/avaliacao \
  -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" \
  -d '{
    "responsavelId": "'$RESP_ID'",
    "notaGeral": 5,
    "notaAtendimento": 5,
    "notaPontualidade": 4,
    "comentario": "Excelente atendimento, super atenciosa"
  }' | jq -r .id)

echo "Avaliacao: $AVALIACAO_ID"

curl -s -X POST $BASE/api/avaliacoes/$AVALIACAO_ID/resposta \
  -H "Authorization: Bearer $TOKEN_VET" -H "Content-Type: application/json" \
  -d '{"resposta":"Muito obrigada pelo carinho com o Rex!"}' | jq
```

**Checklist:** avaliação `201 Created` (dentro da janela de 7 dias — RN-076). Resposta
`200 OK`, `respostaVeterinario` preenchida. Nota pública do vet ainda não aparece
(precisa de 3 avaliações — RN-078):

```bash
curl -s $BASE/api/veterinarios/$VET_ID \
  -H "Authorization: Bearer $TOKEN_ADMIN" | jq '.notaMedia, .totalAvaliacoes'
# esperado: null, 1 — notaMedia só é exposta com totalAvaliacoes >= 3
```

---

## Passo 14 — Dashboard financeiro consolidado (Admin)

```bash
curl -s $BASE/api/empresas/$EMPRESA_ID/dashboard \
  -H "Authorization: Bearer $TOKEN_ADMIN" | jq
```

**Checklist:** `200 OK` — `faturamentoBruto: 150`, `totalComissoes: 18`,
`totalRepasses: 132`, `totalReembolsos: 0`, `qtdConsultasRealizadas: 1`,
`qtdConsultasCanceladas: 0`. Repare que **nenhum campo** de dado bancário pessoal ou
remuneração individual do vet aparece — vedação por construção do DTO (RN-007).

Teste a vedação de posse: gere um token Admin de **outra** empresa (`entidadeId`
qualquer diferente de `$EMPRESA_ID`) e repita a chamada — espera-se `403 ACESSO-002`.

---

## Passo 15 — Cancelamento com reembolso (segunda consulta, para não desfazer o fluxo acima)

Agende uma segunda consulta e cancele-a mais de 24h antes do horário marcado, para ver
o reembolso integral (RN-019):

```bash
CONSULTA2_ID=$(curl -s -X POST $BASE/api/consultas \
  -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" \
  -d '{"dataHora":"2026-12-31T10:00:00Z","modalidade":"Remoto","tipoServico":"Retorno","veterinarioId":"'$VET_ID'","animalId":"'$ANIMAL_ID'","responsavelId":"'$RESP_ID'"}' \
  | jq -r .id)

curl -s -X POST $BASE/api/pagamentos/simular \
  -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" \
  -d '{"consultaId":"'$CONSULTA2_ID'","valor":100.00,"meio":"Pix"}' -o /dev/null -w "pagamento: %{http_code}\n"

curl -s -X DELETE $BASE/api/consultas/$CONSULTA2_ID \
  -H "Authorization: Bearer $TOKEN_ADMIN" | jq
```

**Checklist:** `200 OK`, `estrategiaAplicada: "Reembolso Integral"`,
`valorReembolso: 100.00`, `janela: ">24h"`, `liquidado: false` (nunca liquidado de
verdade — RN-037/062).

---

## Checklist final — RN → endpoint → resultado

| RN | Endpoint exercitado | Resultado esperado |
|---|---|---|
| RN-011 | `POST /api/veterinarios` | CRMV válido aceito (Passo 3) |
| RN-092 | `GET /api/empresas/{id}/assinatura` | Faixa R$599 com 1 vet (Passo 3) |
| LGPD-001 | `POST /api/consultas` sem consentimento | `422` antes de conceder (Passo 5) |
| RN-041..044 | `POST/GET/DELETE .../consentimentos` | Concede, lista, preserva histórico (Passo 5) |
| RN-069 | `POST /api/animais/{id}/obrigacoes` | 4 obrigações caninas, `OBRIGACAO-002` na 2ª chamada (Passo 6) |
| RN-058 | `POST /api/consultas` → `POST .../simular` | `EmCheckout` → `Confirmada` (Passos 7-8) |
| RN-089 | `POST /api/pagamentos/simular` | Comissão 12% (Profissional) (Passo 8) |
| RN-083/086 | `GET .../briefing`, `GET .../log-acessos` | Acesso concedido + logado (Passo 9) |
| RN-096..099 | `POST .../ia/diagnostico\|protocolo\|decisao` | Sugestão auditada + decisão do vet (Passo 10) |
| RN-024/031 | `POST /api/documentos/...`, `.../assinar` | Documento exige estado final; assinatura por nome digitado (Passo 11) |
| RN-091 | `GET /api/documentos/{id}` | `habilitaDispensacaoControlados: false` (Passo 11) |
| RN-070/074 | `POST .../realizada` → `GET .../fidelidade` | 50 pontos, obrigação cumprida (Passo 12) |
| RN-076..080 | `POST .../avaliacao`, `.../resposta` | Avaliação + resposta única (Passo 13) |
| RN-078 | `GET /api/veterinarios/{id}` | `notaMedia: null` com 1 avaliação (Passo 13) |
| RN-007 | `GET /api/empresas/{id}/dashboard` | KPIs agregados, sem dado sensível (Passo 14) |
| ACESSO-002 | `GET .../dashboard` com Admin de outra empresa | `403` (Passo 14) |
| RN-019/062 | `DELETE /api/consultas/{id}` (>24h) | Reembolso integral, não liquidado (Passo 15) |

Se todos os passos acima retornarem os status/códigos esperados, a jornada ponta a
ponta da Vetly v2 está funcionando de fato — não só nos 237 testes automatizados, mas
também na API rodando de verdade contra o Oracle.
