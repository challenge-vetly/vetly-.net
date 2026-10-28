#!/usr/bin/env python3
"""
Percorre a API inteira contra um ambiente de verdade e diz o que responde o que.

Nao substitui a suite de testes: a suite prova regra de negocio isolada, e este
script prova que o conjunto sobe, se autentica, encadeia e responde pelo caminho que
o app do Responsavel e o do veterinario percorrem de fato.

Segue a ordem do ROTEIRO-POSTMAN.md (fluxos 1 a 8) e depois cobre as rotas que o
roteiro nao exercita. Monta estado de verdade -- Admin, veterinarios, Responsavel,
animal, consultas, pagamentos, documentos -- porque a maioria dos endpoints so e
alcancavel depois de outro endpoint ter respondido. No fim, confere a cobertura contra
o /openapi/v1.json do proprio servidor: rota nova sem passo aqui aparece no resumo.

Uso:
    VETLY_TOKEN_INTERNO=<Servicos:TokenInterno> \\
        python deploy/validar-endpoints.py https://host <senha-do-admin> [--rapido]

    Em Production o Admin e o administrador inicial (Bootstrap:AdminEmail; troque o
    e-mail com VETLY_ADMIN_EMAIL). Em Development a rota /api/auth/token responde, e o
    script usa o token de desenvolvimento -- a senha pode ser omitida.

    --rapido pula as rotas de IA e a captura de audio, que dependem do Ollama e levam
    de 30 a 90 segundos cada em CPU. O atendimento segue pelo prontuario manual.

Cria dados de teste no banco do ambiente (e-mails val-*@...). Sai com codigo 1 se
algum passo responder diferente do esperado.
"""

import json
import os
import re
import sys
import time
import uuid
from datetime import datetime, timedelta, timezone
from urllib.parse import urlsplit

import requests

ARGS = [a for a in sys.argv[1:] if not a.startswith("--")]
BASE = (ARGS[0] if ARGS else "http://localhost:5140").rstrip("/")
SENHA_ADMIN = ARGS[1] if len(ARGS) > 1 else None
RAPIDO = "--rapido" in sys.argv
EMAIL_ADMIN = os.environ.get("VETLY_ADMIN_EMAIL", "admin@vetly.com.br")
TOKEN_INTERNO = os.environ.get("VETLY_TOKEN_INTERNO")

if not TOKEN_INTERNO:
    # Sem o webhook nenhuma consulta sai de EmCheckout, e o resto do roteiro viraria
    # uma cascata de 422 que nao prova nada.
    sys.exit("Defina VETLY_TOKEN_INTERNO com o valor de Servicos:TokenInterno do ambiente.")

INICIO = datetime.now(timezone.utc).replace(microsecond=0)
MARCA = int(time.time())
R = []          # (passo, verbo, template, caminho, esperado, obtido, ok, resumo)

OPENAPI = requests.get(BASE + "/openapi/v1.json", timeout=60).json()
TEMPLATES = [(p, m.upper()) for p, v in OPENAPI["paths"].items() for m in v
             if m in ("get", "post", "put", "delete", "patch")]


def template_de(verbo, caminho):
    """Rota do OpenAPI que casa com o caminho chamado; a mais especifica vence."""
    caminho = caminho.split("?")[0]
    candidatos = []
    for p, m in TEMPLATES:
        if m != verbo:
            continue
        # A chave do storage tem barras ("audioconsulta/2026/10/..."): casa com tudo
        rx = "^" + re.sub(r"\{[^}]+\}", "[^/]+", p).replace("/api/storage/[^/]+", "/api/storage/.+") + "$"
        if re.match(rx, caminho):
            candidatos.append((p.count("{"), p))
    return sorted(candidatos)[0][1] if candidatos else caminho


def resumo(r):
    """Uma linha com o que importa da resposta."""
    try:
        d = r.json()
    except Exception:                                         # noqa: BLE001
        return r.text[:120]
    if r.status_code >= 400 and isinstance(d, dict):
        return " | ".join(str(x) for x in [d.get("codigo"), d.get("detail") or d.get("title"),
                                             d.get("erro"), d.get("errors")] if x)[:160]
    if isinstance(d, list):
        return f"lista[{len(d)}]"
    if isinstance(d, dict):
        partes = [f"{k}={str(d[k])[:30]}" for k in
                  ["status", "statusConsulta", "statusPagamento", "role", "valor", "estado",
                   "estadoDaSessao", "saldo", "configurado", "fecha", "total", "decisao", "versao"]
                  if k in d]
        if "_links" in d:
            partes.append("_links=" + ",".join(l.get("rel", "?") for l in d["_links"][:12]))
        return " ".join(partes)[:160]
    return str(d)[:120]


def chamar(passo, verbo, caminho, token=None, corpo=None, esperado=200, idem=None, cab=None,
           timeout=180, bruto=None):
    """Faz a chamada, registra o desfecho e devolve a resposta (ou None)."""
    h = {}
    if corpo is not None:
        h["Content-Type"] = "application/json"
    if token:
        h["Authorization"] = f"Bearer {token}"
    if idem:
        h["Idempotency-Key"] = idem if isinstance(idem, str) else str(uuid.uuid4())
    if cab:
        h.update(cab)
    aceitos = esperado if isinstance(esperado, (list, tuple)) else [esperado]
    try:
        r = requests.request(verbo, BASE + caminho, headers=h, timeout=timeout,
                             data=bruto if bruto is not None else
                             (json.dumps(corpo) if corpo is not None else None))
    except Exception as e:                                    # noqa: BLE001
        registrar(passo, verbo, caminho, aceitos, 0, f"EXCECAO {type(e).__name__}")
        return None
    registrar(passo, verbo, caminho, aceitos, r.status_code, resumo(r))
    return r


def registrar(passo, verbo, caminho, aceitos, obtido, detalhe=""):
    ok = obtido in aceitos
    R.append((passo, verbo, template_de(verbo, caminho), caminho, aceitos, obtido, ok, detalhe))
    print(f"  [{'ok  ' if ok else 'FALHA'}] {passo:5} {verbo:6} {caminho[:64]:64} {obtido}  {detalhe[:70]}")
    return ok


def conferir(passo, descricao, condicao, detalhe=""):
    """Verificacao de conteudo, alem do status."""
    R.append((passo, "CHECK", descricao, descricao, ["sim"], "sim" if condicao else "nao",
              bool(condicao), detalhe))
    print(f"  [{'ok  ' if condicao else 'FALHA'}] {passo:5} CHECK  {descricao[:64]:64} {detalhe[:70]}")


def j(r):
    if r is None:
        return {}
    try:
        return r.json()
    except Exception:                                         # noqa: BLE001
        return {}


def itens(d):
    """Lista de uma resposta que pode vir crua ou paginada ({itens: [...]})."""
    return d if isinstance(d, list) else (d or {}).get("itens", [])


def rels(r):
    return [l["rel"] for l in j(r).get("_links", [])] if isinstance(j(r), dict) else []


def caminho_de(url):
    """Caminho+query de uma URL absoluta, para chamar pelo BASE."""
    partes = urlsplit(url)
    return partes.path + (f"?{partes.query}" if partes.query else "")


def slots_livres(vet, tk, pular=0):
    d = j(requests.get(f"{BASE}/api/veterinarios/{vet}/disponibilidade",
                       headers={"Authorization": f"Bearer {tk}"}, timeout=60))
    return [h["id"] for dia in d.get("dias", []) for h in dia.get("horarios", [])][pular:]


def webhook(ref, status="Confirmado"):
    return requests.post(f"{BASE}/api/internos/pagamentos/webhook",
                         headers={"X-Vetly-Service-Token": TOKEN_INTERNO},
                         json={"referenciaExterna": ref, "status": status}, timeout=60)


def consulta_paga(tk, tid, animal, vet, servico, pular=0, confirmar=True):
    """Checkout + cobranca (+ webhook), o atalho dos cenarios do fluxo 7."""
    s = slots_livres(vet, tk, pular)
    c = j(requests.post(f"{BASE}/api/consultas/checkout", headers={
        "Authorization": f"Bearer {tk}", "Idempotency-Key": str(uuid.uuid4())}, json={
        "animalId": animal, "prestadorId": vet, "slotId": s[0], "servicoId": servico},
        timeout=60)).get("consultaId")
    p = j(requests.post(f"{BASE}/api/pagamentos", headers={
        "Authorization": f"Bearer {tk}", "Idempotency-Key": str(uuid.uuid4())}, json={
        "tutorId": tid, "consultaId": c, "valor": 200.0, "meioPagamento": "Pix"}, timeout=60))
    ref = (p.get("instrucoes") or {}).get("referenciaExterna")
    if confirmar and ref:
        webhook(ref)
    return c, p.get("id"), ref


def tutor_novo(nome):
    r = requests.post(f"{BASE}/api/auth/registro/tutor", json={
        "nome": nome, "email": f"val-{nome.lower()}-{MARCA}@exemplo.com",
        "telefone": "11999990000", "senha": "senha-forte-123"}, timeout=60)
    d = j(r)
    tk, tid = d.get("token"), d.get("tutorId")
    requests.put(f"{BASE}/api/tutores/{tid}/consentimentos", headers={"Authorization": f"Bearer {tk}"},
                 json={"consentimentos": [{"finalidade": "Atendimento", "concedido": True}]}, timeout=60)
    return tk, tid


def vet_novo(adm, nome, persona="Autonomo", plano="Profissional", cep=None, desloc=0):
    """Cadastra pelo Admin, loga com a senha temporaria e devolve (id, token, senha)."""
    email = f"val-{nome.split()[0].lower()}-{MARCA}@clinica.com"
    corpo = {"nome": nome, "crmv": f"{10000 + (MARCA + desloc) % 89999}-SP", "ufAtuacao": "SP",
             "email": email, "persona": persona, "plano": plano, "especialidades": ["Clinica Geral"],
             "especiesAtendidas": ["Canino", "Felino"]}
    if cep:
        corpo["endereco"] = {"cep": cep, "logradouro": "Avenida Paulista", "numero": "1000",
                             "bairro": "Bela Vista", "cidade": "Sao Paulo", "uf": "SP"}
    r = chamar("adm", "POST", "/api/veterinarios", token=adm, esperado=201, corpo=corpo)
    d = j(r)
    vid, senha = (d.get("veterinario") or {}).get("id"), d.get("senhaTemporaria")
    sessao = j(requests.post(f"{BASE}/api/auth/login", json={"email": email, "senha": senha}, timeout=60))
    return vid, sessao.get("token"), senha, email


def agenda_e_servicos(vid, tk, valor=200.0):
    requests.put(f"{BASE}/api/veterinarios/{vid}/agenda-config", headers={"Authorization": f"Bearer {tk}"},
                 json={"dias": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"],
                       "horaInicio": "08:00", "horaFim": "18:00", "duracaoMinutos": 30,
                       "intervaloMinutos": 0}, timeout=120)
    s = j(requests.put(f"{BASE}/api/veterinarios/{vid}/servicos", headers={"Authorization": f"Bearer {tk}"},
                       json={"servicos": [{"tipo": "ConsultaRotina", "valor": valor, "duracaoMinutos": 30,
                                           "aceitaPlanoPet": False}]}, timeout=60))
    return s[0]["id"] if isinstance(s, list) and s else None


def secao(titulo):
    print(f"\n=== {titulo}")


# ═════════════════════════════════════════════════════════════════════════════
secao("Fluxo 8 -- observabilidade (antes)")

for rota in ["/health", "/health/live", "/health/ready", "/metrics", "/openapi/v1.json"]:
    chamar("8", "GET", rota)

r = chamar("8", "GET", "/api/veterinarios", cab={"X-Correlation-Id": f"val-{MARCA}"}, esperado=401)
conferir("8", "X-Correlation-Id devolvido", r is not None and r.headers.get("X-Correlation-Id") == f"val-{MARCA}")

esquemas = OPENAPI.get("components", {}).get("schemas", {})
persona = (esquemas.get("CriarVeterinarioDto", {}).get("properties", {}).get("persona") or {})
if "$ref" in persona:
    persona = esquemas.get(persona["$ref"].split("/")[-1], {})
conferir("8", "OpenAPI documenta enum como texto", "Autonomo" in (persona.get("enum") or []))

# ═════════════════════════════════════════════════════════════════════════════
secao("Fluxo 1 -- preparar o prestador")

r = requests.post(f"{BASE}/api/auth/token", json={"usuario": "validador", "role": "Admin"}, timeout=60)
DEV = r.status_code == 200

if DEV:
    registrar("1.1", "POST", "/api/auth/token", [200], r.status_code, "Development: token de Admin")
    ADM = r.json()["token"]
else:
    # Fora de Development a rota nao existe: emitir token sem credencial seria porta aberta
    registrar("1.1", "POST", "/api/auth/token", [404], r.status_code)
    r = chamar("1.1", "POST", "/api/auth/login", corpo={"email": EMAIL_ADMIN, "senha": SENHA_ADMIN or ""})
    ADM = j(r).get("token")
    conferir("1.1", "login do administrador devolve role Admin", j(r).get("role") == "Admin", str(j(r).get("role")))
    if not ADM:
        sys.exit("Sem token de Admin nao ha como continuar. Informe a senha do administrador inicial.")

VET, VT, SENHA_VET, EMAIL_VET = vet_novo(ADM, "Marina Alves", cep="01310-100")
r = requests.post(f"{BASE}/api/auth/login", json={"email": EMAIL_VET, "senha": SENHA_VET}, timeout=60)
conferir("1.3", "primeiro login avisa senha temporaria", j(r).get("senhaTemporaria") is True)

r = chamar("1.4", "PUT", f"/api/veterinarios/{VET}/agenda-config", token=VT, corpo={
    "dias": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"],
    "horaInicio": "08:00", "horaFim": "18:00", "duracaoMinutos": 30, "intervaloMinutos": 0})
cfg = j(chamar("1.4", "GET", f"/api/veterinarios/{VET}/agenda-config", token=VT))
conferir("1.4", "horario persistido sem truncamento",
         cfg.get("horaInicio") == "08:00" and cfg.get("horaFim") == "18:00",
         f"{cfg.get('horaInicio')}-{cfg.get('horaFim')}")

r = chamar("1.5", "PUT", f"/api/veterinarios/{VET}/servicos", token=VT, corpo={"servicos": [
    {"tipo": "ConsultaRotina", "valor": 200.0, "duracaoMinutos": 30, "aceitaPlanoPet": False},
    {"tipo": "Cirurgia", "valor": 1500.0, "duracaoMinutos": 300, "aceitaPlanoPet": False}]})
servicos = j(r) if isinstance(j(r), list) else []
SRV = next((s["id"] for s in servicos if s.get("tipo") == "ConsultaRotina"), None)
conferir("1.5", "duracao de 300 minutos sem truncamento",
         any(s.get("duracaoMinutos") == 300 for s in servicos))

chamar("1.6", "GET", "/api/veterinarios/me/dados-repasse", token=VT)
chamar("1.6", "PUT", "/api/veterinarios/me/dados-repasse", token=VT, corpo={
    "banco": "341", "agencia": "1234", "conta": "0012345-6",
    "documentoTitular": "123.456.789-09", "chavePix": f"marina{MARCA}@vetly.com"})
chamar("1.6", "PUT", "/api/veterinarios/me/dados-repasse", token=VT, esperado=400, corpo={
    "banco": "341", "agencia": "1", "conta": "1", "documentoTitular": "123", "chavePix": "x"})
repasse = j(chamar("1.6", "GET", "/api/veterinarios/me/dados-repasse", token=VT))
conferir("1.6", "conta de repasse mascarada", "*" in str(repasse.get("conta")))

# ═════════════════════════════════════════════════════════════════════════════
secao("Fluxo 2 -- onboarding do Responsavel")

EMAIL_TUTOR = f"val-ana-{MARCA}@exemplo.com"
r = chamar("2.1", "POST", "/api/auth/registro/tutor", esperado=201, corpo={
    "nome": "Ana Souza", "email": EMAIL_TUTOR, "telefone": "11999998888", "senha": "senha-forte-123"})
d = j(r)
TK, TID, REFRESH = d.get("token"), d.get("tutorId"), d.get("refreshToken")
conferir("2.1", "sessao nasce com consentimento pendente", d.get("consentimentoPendente") is True)

chamar("7.7", "GET", "/api/animais", token=TK, esperado=422)        # RN-060
chamar("2.2", "GET", f"/api/tutores/{TID}/consentimentos", token=TK)
chamar("2.2", "PUT", f"/api/tutores/{TID}/consentimentos", token=TK, corpo={"consentimentos": [
    {"finalidade": f, "concedido": True} for f in ("Atendimento", "Lembretes", "Compartilhamento", "Promocoes")]})

r = chamar("2.3", "POST", "/api/animais", token=TK, esperado=201, corpo={
    "nome": "Thor", "especie": "Canino", "raca": "Golden Retriever",
    "dataNascimento": "2023-04-10T00:00:00Z", "tutorId": TID, "pesoKg": 31.5, "sexo": "Macho",
    "castrado": True, "alergias": ["Dipirona"], "condicoesPreexistentes": [],
    "carteiraVacinacao": [{"tipo": "V10", "aplicadaEm": "2025-03-15T00:00:00Z"}]})
ANI = j(r).get("id")

r = chamar("2.4", "POST", f"/api/tutores/{TID}/dispositivos", token=TK, esperado=[200, 201],
           corpo={"pushToken": f"fcm-val-{MARCA}", "plataforma": "Android"})
DISP = j(r).get("id")
r = chamar("2.4", "POST", f"/api/tutores/{TID}/dispositivos", token=TK, esperado=[200, 201],
           corpo={"pushToken": f"fcm-val-{MARCA}", "plataforma": "Android"})
conferir("2.4", "reinstalacao reaproveita o dispositivo", j(r).get("id") == DISP)

# ═════════════════════════════════════════════════════════════════════════════
secao("Fluxo 3 -- buscar e agendar")

r = chamar("3.1", "GET", f"/api/busca?animalId={ANI}&lat=-23.561414&lng=-46.655881&raioKm=10", token=TK)
conferir("3.1", "a busca encontra o veterinario pelo CEP geocodificado",
         any(x.get("prestadorId") == VET for x in itens(j(r))),
         "lista vazia: confira TB_CEP_COORDENADA (01310100)")
chamar("3.1", "GET", f"/api/busca?animalId={ANI}&cep=01310-100&raioKm=10", token=TK)
chamar("3.1", "GET", "/api/busca?lat=-23.56&lng=-46.65", token=TK, esperado=400)

r = chamar("3.2", "GET", f"/api/veterinarios/{VET}/disponibilidade", token=TK)
SLOTS = [h["id"] for dia in j(r).get("dias", []) for h in dia.get("horarios", [])]

chamar("7.8", "POST", "/api/consultas/checkout", token=TK, esperado=400, corpo={
    "animalId": ANI, "prestadorId": VET, "slotId": SLOTS[0], "servicoId": SRV})
r = chamar("3.3", "POST", "/api/consultas/checkout", token=TK, esperado=201, idem=True, corpo={
    "animalId": ANI, "prestadorId": VET, "slotId": SLOTS[0], "servicoId": SRV})
CON = j(r).get("consultaId")
chamar("7.4", "POST", "/api/consultas/checkout", token=TK, esperado=409, idem=True, corpo={
    "animalId": ANI, "prestadorId": VET, "slotId": SLOTS[0], "servicoId": SRV})

chave = str(uuid.uuid4())
r = chamar("3.4", "POST", "/api/pagamentos", token=TK, esperado=202, idem=chave, corpo={
    "tutorId": TID, "consultaId": CON, "valor": 1.0, "meioPagamento": "Pix"})
PAG = j(r)
PAGID, REF = PAG.get("id"), (PAG.get("instrucoes") or {}).get("referenciaExterna")
conferir("3.4", "o preco sai do servico, nao do corpo", PAG.get("valor") == 200, f"cobrado {PAG.get('valor')}")
r2 = chamar("7.5", "POST", "/api/pagamentos", token=TK, esperado=202, idem=chave, corpo={
    "tutorId": TID, "consultaId": CON, "valor": 1.0, "meioPagamento": "Pix"})
conferir("7.5", "reenvio idempotente devolve o corpo identico", r is not None and r2 is not None and r.text == r2.text)

chamar("3.5", "POST", "/api/internos/pagamentos/webhook", esperado=401,
       corpo={"referenciaExterna": REF, "status": "Confirmado"})
r = chamar("3.5", "POST", "/api/internos/pagamentos/webhook", cab={"X-Vetly-Service-Token": TOKEN_INTERNO},
           corpo={"referenciaExterna": REF, "status": "Confirmado"})
conferir("3.5", "webhook confirma a consulta", j(r).get("statusConsulta") == "Confirmada")

r = chamar("7.13", "GET", f"/api/notificacoes/tutor/{TID}", token=TK)
conferir("7.13", "aviso ConsultaConfirmada na caixa", any(n.get("tipo") == "ConsultaConfirmada" for n in itens(j(r))))

chamar("3.6", "PUT", f"/api/consultas/{CON}/pre-sintomas", token=TK, esperado=[200, 204], corpo={
    "queixaPrincipal": "Vomito ha dois dias", "duracaoEmDias": 2,
    "sinaisObservados": ["Apatia", "Recusa alimentar"], "alimentacaoNormal": False,
    "mudancaDeComportamento": True, "observacoes": "Bebeu pouca agua ontem."})

# ═════════════════════════════════════════════════════════════════════════════
secao("Fluxo 4 -- o atendimento")

chamar("4.1", "GET", "/api/dashboard/veterinario", token=VT)
chamar("4.2", "GET", f"/api/consultas/{CON}/briefing", token=VT)
chamar("4.3", "POST", f"/api/consultas/{CON}/iniciar", token=VT)
chamar("4.3", "POST", f"/api/consultas/{CON}/iniciar", token=VT, esperado=409)

r = chamar("4.4", "POST", "/api/midia/upload-url", token=VT, esperado=[200, 201], corpo={
    "tipo": "AudioConsulta", "contentType": "audio/wav", "consultaId": CON})
up = j(r)
MIDIA = up.get("midiaId")
n = 32000
wav = (b"RIFF" + (36 + n).to_bytes(4, "little") + b"WAVEfmt " + (16).to_bytes(4, "little") +
       (1).to_bytes(2, "little") + (1).to_bytes(2, "little") + (16000).to_bytes(4, "little") +
       (32000).to_bytes(4, "little") + (2).to_bytes(2, "little") + (16).to_bytes(2, "little") +
       b"data" + n.to_bytes(4, "little") + b"\x00" * n)
if up.get("uploadUrl"):
    chamar("4.4", "PUT", caminho_de(up["uploadUrl"]), bruto=wav, cab={"Content-Type": "audio/wav"},
           esperado=[200, 201, 204])
    u = j(chamar("4.4", "GET", f"/api/midia/{MIDIA}/url", token=VT)).get("url")
    if u:
        r = chamar("4.4", "GET", caminho_de(u))
        conferir("4.4", "a URL assinada devolve o audio enviado", r is not None and r.content == wav)

pronto = False
if not RAPIDO:
    chamar("4.4", "POST", f"/api/consultas/{CON}/captura/segmentos", token=VT, esperado=202, corpo={
        "sequencia": 0, "midiaId": MIDIA, "duracaoMs": 30000, "inicioRelativoMs": 0})
    chamar("4.4", "POST", f"/api/consultas/{CON}/captura/segmentos", token=VT, esperado=409, corpo={
        "sequencia": 0, "midiaId": MIDIA, "duracaoMs": 30000, "inicioRelativoMs": 0})
    time.sleep(8)
chamar("4.5", "GET", f"/api/consultas/{CON}/captura", token=VT)
chamar("4.6", "POST", f"/api/consultas/{CON}/encerrar", token=VT)

if not RAPIDO:
    for _ in range(40):
        rr = requests.get(f"{BASE}/api/consultas/{CON}/rascunho",
                          headers={"Authorization": f"Bearer {VT}"}, timeout=60)
        if rr.status_code == 200:
            pronto = True
            break
        time.sleep(15)
    # Sem transcricao (audio ilegivel, motor fora) a sessao vai a um estado terminal e
    # o caminho e o prontuario manual (RN-085) -- desfecho legitimo, nao falha.
    estado = j(requests.get(f"{BASE}/api/consultas/{CON}/captura",
                            headers={"Authorization": f"Bearer {VT}"}, timeout=60)).get("estado")
    if pronto:
        chamar("4.7", "GET", f"/api/consultas/{CON}/rascunho", token=VT)
        chamar("4.8", "PUT", f"/api/consultas/{CON}/validar-diagnostico", token=VT, corpo={
            "decisao": "NaoAprovado", "justificativa": "O rascunho nao reflete o exame fisico realizado."})
    else:
        print(f"        sem rascunho; sessao em {estado}")

CONTEUDO = {"anamnese": "Vomito ha dois dias, sem diarreia.",
            "exameFisico": "Hidratado, mucosas normocoradas, abdome indolor a palpacao.",
            "hipotesesDiagnosticas": ["Gastrite alimentar"],
            "conduta": "Dieta branda por cinco dias.", "orientacoes": "Retornar se o vomito persistir."}
chamar("4.9", "POST", f"/api/consultas/{CON}/prontuario-manual", token=VT, esperado=[200, 201],
       corpo={"conteudo": CONTEUDO})
chamar("4.10", "GET", f"/api/consultas/{CON}/auditoria-ia", token=VT)

# ═════════════════════════════════════════════════════════════════════════════
secao("Fluxo 5 -- documentos")

DOC = j(chamar("5.1", "POST", f"/api/documentos/consulta/{CON}?tipo=Prontuario", token=VT, esperado=201)).get("id")
REC = j(chamar("7.9", "POST", f"/api/documentos/consulta/{CON}?tipo=ReceitaVeterinaria", token=VT,
               esperado=201)).get("id")
chamar("7.9", "POST", f"/api/documentos/{REC}/publicar", token=VT, esperado=422)      # RN-087
ATE = j(chamar("5.1", "POST", f"/api/documentos/consulta/{CON}?tipo=Atestado&subtipo=Saude", token=VT,
               esperado=201)).get("id")
chamar("5.1", "POST", f"/api/documentos/consulta/{CON}?tipo=NotaFiscal", token=VT, esperado=201)

chamar("5.2", "POST", f"/api/documentos/{DOC}/assinar", token=VT, esperado=422, corpo={"nomeCompleto": "Outra Pessoa"})
for doc in (DOC, REC, ATE):
    chamar("5.2", "POST", f"/api/documentos/{doc}/assinar", token=VT, corpo={"nomeCompleto": "Marina Alves"})
chamar("5.3", "POST", f"/api/documentos/{DOC}/publicar", token=VT)
chamar("5.3", "POST", f"/api/documentos/{REC}/publicar", token=VT)
chamar("5.x", "GET", f"/api/documentos/{DOC}", token=VT)
conferir("HAL", "documento: veterinario recebe o link de correcao",
         "corrigir" in rels(chamar("HAL", "GET", f"/api/documentos/{DOC}?hateoas=true", token=VT)))
r = chamar("HAL", "GET", f"/api/documentos/consulta/{CON}", token=VT, cab={"Accept": "application/hal+json"})
conferir("HAL", "colecao de documentos em hal+json", r is not None and "_links" in r.text)

chamar("5.4", "POST", f"/api/consultas/{CON}/finalizar", token=VT)
chamar("5.5", "GET", f"/api/documentos/animal/{ANI}", token=TK)
conferir("HAL", "documento: Responsavel recebe marcar-lido",
         "marcar-lido" in rels(chamar("HAL", "GET", f"/api/documentos/{DOC}?hateoas=true", token=TK)))
chamar("5.6", "POST", f"/api/documentos/{DOC}/lido", token=TK, esperado=[200, 204])

historico = itens(j(chamar("5.x", "GET", f"/api/animais/{ANI}/prontuarios", token=TK)))
conferir("5.3", "prontuario publicado entra no historico longitudinal", len(historico) == 1, f"{len(historico)} registro(s)")

r = chamar("5.7", "POST", f"/api/documentos/{DOC}/correcao", token=VT, esperado=[200, 201], corpo={
    "novosDados": "Conteudo corrigido do documento.", "justificativa": "Correcao da dosagem prescrita."})
COR = j(r)
conferir("5.7", "correcao e a versao 2 e guarda a justificativa",
         COR.get("versao") == 2 and COR.get("justificativaCorrecao") == "Correcao da dosagem prescrita.",
         f"versao={COR.get('versao')}")
chamar("5.7", "POST", f"/api/documentos/{DOC}/correcao", token=VT, esperado=400, corpo={
    "novosDados": "x", "justificativa": "x" * 1001})
chamar("5.7", "POST", f"/api/documentos/{COR.get('id')}/publicar", token=VT)
historico = itens(j(requests.get(f"{BASE}/api/animais/{ANI}/prontuarios",
                                 headers={"Authorization": f"Bearer {TK}"}, timeout=60)))
conferir("5.7", "correcao publicada entra no historico como nova versao",
         len(historico) == 2 and historico[0].get("versaoOriginalId") == historico[1].get("id"),
         f"{len(historico)} registro(s)")

# ═════════════════════════════════════════════════════════════════════════════
secao("Fluxo 6 -- depois da consulta")

AVA = j(chamar("6.1", "POST", f"/api/avaliacoes/consulta/{CON}", token=TK, esperado=[200, 201], corpo={
    "nota": 5, "comentario": "Atendimento atencioso e explicacao clara."})).get("id")
chamar("6.1", "POST", f"/api/avaliacoes/consulta/{CON}", token=TK, esperado=[409, 422], corpo={"nota": 4})
rep = j(chamar("6.2", "GET", f"/api/avaliacoes/veterinario/{VET}", token=TK))
conferir("6.2", "nota 5 gravada sem truncamento", (rep.get("distribuicao") or {}).get("5", 0) >= 1,
         f"distribuicao={rep.get('distribuicao')}")
conferir("6.2", "nota nao e publica com menos de 3 avaliacoes", rep.get("notaPublica") is False)
chamar("6.3", "POST", f"/api/avaliacoes/{AVA}/resposta", token=VT, esperado=[200, 204],
       corpo={"resposta": "Obrigada pela confianca!"})
chamar("6.3", "POST", f"/api/avaliacoes/{AVA}/resposta", token=VT, esperado=[409, 422], corpo={"resposta": "De novo."})

# Os pontos da consulta realizada sao creditados pelo job (ciclo de 30 s)
for _ in range(12):
    if (j(requests.get(f"{BASE}/api/fidelidade/saldo", headers={"Authorization": f"Bearer {TK}"},
                       timeout=60)).get("saldo") or 0) >= 200:
        break
    time.sleep(10)
chamar("6.4", "GET", "/api/fidelidade/saldo", token=TK)
item = {"itemRef": "racao-premium-2kg", "itemNome": "Racao Premium 2kg", "categoria": "Alimentacao", "pontos": 200}
chamar("6.5", "POST", "/api/fidelidade/resgates/simular", token=TK, esperado=[200, 422], corpo=item)
QR = j(chamar("6.6", "POST", "/api/fidelidade/resgates", token=TK, esperado=[201, 422], idem=True,
              corpo=item)).get("codigoQr")
chamar("6.7", "GET", f"/api/notificacoes/tutor/{TID}", token=TK)
chamar("6.8", "GET", f"/api/animais/{ANI}/board", token=TK)
chamar("6.9", "POST", f"/api/consultas/{CON}/retorno", token=VT, esperado=[200, 201], idem=True, corpo={
    "slotId": slots_livres(VET, TK, 6)[0], "motivo": "Reavaliacao apos cinco dias de dieta."})

# ═════════════════════════════════════════════════════════════════════════════
secao("Fluxo 7 -- cenarios")

C2, P2, _ = consulta_paga(TK, TID, ANI, VET, SRV, pular=10)
chamar("7.1", "GET", f"/api/consultas/{C2}/simulacao-cancelamento", token=TK)
chamar("7.8", "DELETE", f"/api/consultas/{C2}", token=TK, esperado=400)
chamar("7.2", "DELETE", f"/api/consultas/{C2}", token=TK, esperado=[200, 204], idem=True)
time.sleep(2)
r = chamar("7.14", "GET", f"/api/notificacoes/tutor/{TID}", token=TK)
conferir("7.14", "aviso de reembolso na caixa", any(n.get("tipo") == "ReembolsoConfirmado" for n in itens(j(r))))
chamar("7.2", "GET", f"/api/pagamentos/{P2}/status", token=TK)

C3, _, _ = consulta_paga(TK, TID, ANI, VET, SRV, pular=14)
for i, esp in enumerate([200, 200, 422]):                                 # RN-043
    chamar("7.3", "POST", f"/api/consultas/{C3}/remarcar", token=TK, esperado=esp, idem=True,
           corpo={"novoSlotId": slots_livres(VET, TK, 20 + i * 2)[0]})

slot_recusado = slots_livres(VET, TK, 30)[0]
_, _, REF4 = consulta_paga(TK, TID, ANI, VET, SRV, pular=30, confirmar=False)
r = chamar("3.5", "POST", "/api/internos/pagamentos/webhook", cab={"X-Vetly-Service-Token": TOKEN_INTERNO},
           corpo={"referenciaExterna": REF4, "status": "Recusado"})
conferir("3.5", "pagamento recusado expira a consulta e libera o horario",
         j(r).get("statusConsulta") == "Expirada" and slot_recusado in slots_livres(VET, TK))

TK2, TID2 = tutor_novo("Intruso")
for rota in [f"/api/consultas/{CON}", f"/api/animais/{ANI}", f"/api/animais/{ANI}/board",
             f"/api/animais/{ANI}/avatar", f"/api/tutores/{TID}/carteira",
             f"/api/tutores/{TID}/carteira/historico", f"/api/documentos/{DOC}"]:
    chamar("7.6", "GET", rota, token=TK2, esperado=403)

chamar("7.10", "GET", "/api/financeiro/consolidado", token=ADM)
chamar("7.10", "GET", "/api/financeiro/consolidado", token=TK, esperado=403)

VET2, VT2, _, EMAIL_VET2 = vet_novo(ADM, "Paulo Externo", plano="Basico", desloc=7)
chamar("7.11", "GET", f"/api/animais/{ANI}/prontuarios", token=VT2, esperado=403)
COL = j(chamar("7.11", "POST", "/api/colmeia", token=TK, esperado=201, corpo={
    "animalId": ANI, "veterinarioId": VET2, "escopo": "HistoricoCompleto", "validadeEmDias": 30,
    "motivo": "Atendimento de emergencia em outra clinica."})).get("id")
chamar("7.11", "GET", f"/api/animais/{ANI}/prontuarios", token=VT2)
chamar("7.11", "GET", f"/api/colmeia/animal/{ANI}", token=TK)
chamar("7.11", "GET", f"/api/colmeia/animal/{ANI}/acessos", token=TK)
chamar("7.11", "DELETE", f"/api/colmeia/{COL}", token=TK, esperado=[200, 204])
chamar("7.11", "GET", f"/api/animais/{ANI}/prontuarios", token=VT2, esperado=403)
chamar("7.12", "GET", f"/api/animais/{ANI}/acessos", token=TK)

# Unidade: uma gestora administra a empresa e um vinculado atende por ela
GESTORA, _, SENHA_GESTORA, EMAIL_GESTORA = vet_novo(ADM, "Clara Gestora", plano="Enterprise", desloc=13)
EMP = j(chamar("7.16", "POST", "/api/empresas", token=ADM, esperado=201, corpo={
    "nome": f"Clinica Validacao {MARCA}", "tipo": "Clinica", "administradorId": GESTORA,
    "plano": "Enterprise"})).get("id")
VINC, VT3, _, _ = vet_novo(ADM, "Bruno Vinculado", persona="Vinculado", cep="04538-133", desloc=29)
chamar("7.16", "POST", f"/api/empresas/{EMP}/veterinarios/{VINC}", token=ADM, esperado=[200, 204])
r = requests.post(f"{BASE}/api/auth/login", json={"email": EMAIL_GESTORA, "senha": SENHA_GESTORA}, timeout=60)
GT = j(r).get("token")
conferir("7.16", "quem administra unidade loga como Admin", j(r).get("role") == "Admin", str(j(r).get("role")))
chamar("7.16", "GET", "/api/dashboard/unidade", token=GT)
chamar("7.17", "GET", "/api/dashboard/unidade", token=VT, esperado=403)
# O Admin de desenvolvimento nao tem cadastro profissional; o do bootstrap tem
chamar("7.18", "GET", "/api/veterinarios/me/dados-repasse", token=ADM, esperado=403 if DEV else 200)

# ═════════════════════════════════════════════════════════════════════════════
secao("Auth e sessao")

chamar("auth", "POST", "/api/auth/registro/tutor", esperado=422, corpo={
    "nome": "Dup", "email": EMAIL_TUTOR, "telefone": "11999998888", "senha": "senha-forte-123"})
chamar("auth", "POST", "/api/auth/login", esperado=422, corpo={"email": EMAIL_TUTOR, "senha": "errada"})
chamar("auth", "GET", "/api/auth/me", token=TK)
chamar("auth", "POST", "/api/auth/trocar-senha", token=VT, esperado=[200, 204],
       corpo={"senhaAtual": SENHA_VET, "novaSenha": "nova-senha-forte-456"})
chamar("auth", "POST", "/api/auth/login", corpo={"email": EMAIL_VET, "senha": "nova-senha-forte-456"})
r = chamar("auth", "POST", "/api/auth/refresh", corpo={"refreshToken": REFRESH})
novo = j(r).get("refreshToken")
chamar("auth", "POST", "/api/auth/refresh", esperado=[401, 422], corpo={"refreshToken": REFRESH})

# ═════════════════════════════════════════════════════════════════════════════
secao("Tutores, animais e obrigacoes")

chamar("tut", "GET", f"/api/tutores/{TID}", token=TK)
chamar("tut", "GET", f"/api/tutores/{TID}/animais", token=TK)
chamar("tut", "GET", f"/api/tutores/{TID}/carteira", token=TK)
chamar("tut", "GET", f"/api/tutores/{TID}/carteira/historico?ordenarPor=data&direcao=desc&hateoas=true", token=TK)
chamar("tut", "GET", "/api/tutores", token=TK, esperado=403)
chamar("tut", "GET", "/api/tutores", token=ADM)
chamar("tut", "PUT", f"/api/tutores/{TID}", token=TK, esperado=[200, 204], corpo={
    "nome": "Ana Souza Lima", "email": EMAIL_TUTOR, "telefone": "11988887777"})
chamar("tut", "GET", f"/api/tutores/{TID}/dispositivos", token=TK)
chamar("tut", "DELETE", f"/api/tutores/{TID}/dispositivos/{DISP}", token=TK, esperado=[200, 204])
TUT_BALCAO = j(chamar("tut", "POST", "/api/tutores", token=ADM, esperado=201, corpo={
    "nome": "Tutor Balcao", "email": f"val-balcao-{MARCA}@exemplo.com", "telefone": "11977776666"})).get("id")

chamar("ani", "GET", "/api/animais", token=TK)
r = chamar("HAL", "GET", "/api/animais", token=TK, cab={"Accept": "application/hal+json"})
conferir("HAL", "colecao de animais em hal+json", r is not None and "_links" in r.text)
chamar("ani", "GET", f"/api/animais/{ANI}", token=TK)
links = rels(chamar("HAL", "GET", f"/api/animais/{ANI}?hateoas=true", token=TK))
conferir("HAL", "animal: o dono recebe atualizar e desativar", {"atualizar", "desativar"} <= set(links))
links = rels(chamar("HAL", "GET", f"/api/animais/{ANI}?hateoas=true", token=VT))
conferir("HAL", "animal: o veterinario nao recebe desativar", links and "desativar" not in links)
for rota in ["obrigacoes", "exames", "avatar", "avatar?hateoas=true", "avatar/historico"]:
    chamar("ani", "GET", f"/api/animais/{ANI}/{rota}", token=TK)
chamar("ani", "PUT", f"/api/animais/{ANI}/peso", token=TK, esperado=[200, 204], corpo={"pesoKg": 32.0})
chamar("ani", "PUT", f"/api/animais/{ANI}", token=TK, esperado=[200, 204], corpo={
    "nome": "Thor", "especie": "Canino", "raca": "Golden Retriever", "dataNascimento": "2023-04-10T00:00:00Z",
    "tutorId": TID, "pesoKg": 32.0, "sexo": "Macho", "castrado": True, "alergias": ["Dipirona"]})
REG = historico[0]["id"] if historico else str(uuid.uuid4())
chamar("ani", "PATCH", f"/api/animais/{ANI}/historico/{REG}/ocultar", token=TK, corpo={"oculto": True})
chamar("ani", "PATCH", f"/api/animais/{ANI}/historico/{REG}/ocultar", token=TK, corpo={"oculto": False})
ANI_DEL = j(chamar("ani", "POST", "/api/animais", token=TK, esperado=201, corpo={
    "nome": "Mia", "especie": "Felino", "raca": "SRD", "dataNascimento": "2024-01-01T00:00:00Z",
    "tutorId": TID, "pesoKg": 4.2, "sexo": "Femea"})).get("id")

vencimento = (datetime.now(timezone.utc) + timedelta(days=20)).isoformat()
OBR = j(chamar("obr", "POST", f"/api/obrigacoes/animal/{ANI}", token=TK, esperado=[200, 201], corpo={
    "tipo": "Vacina", "descricao": "Antirrabica", "proximoVencimento": vencimento,
    "periodicidadeEmDias": 365})).get("id")
chamar("obr", "GET", f"/api/obrigacoes/animal/{ANI}?incluirArquivadas=true", token=TK)
chamar("obr", "POST", f"/api/obrigacoes/animal/{ANI}/derivar-da-carteira", token=TK, esperado=[200, 201])
chamar("obr", "POST", f"/api/obrigacoes/{OBR}/cumprir", token=TK, esperado=[200, 204],
       corpo={"quando": datetime.now(timezone.utc).isoformat()})
OBR2 = j(chamar("obr", "POST", f"/api/obrigacoes/animal/{ANI}", token=TK, esperado=[200, 201], corpo={
    "tipo": "Vermifugo", "descricao": "Vermifugo trimestral", "proximoVencimento": vencimento,
    "periodicidadeEmDias": 90})).get("id")
chamar("obr", "DELETE", f"/api/obrigacoes/{OBR2}", token=TK, esperado=[200, 204])

# ═════════════════════════════════════════════════════════════════════════════
secao("Veterinarios e empresas")

chamar("vet", "GET", "/api/veterinarios", token=TK)
chamar("vet", "GET", "/api/veterinarios/regiao/SP", token=TK)
chamar("vet", "GET", f"/api/veterinarios/{VET}", token=TK)
conferir("HAL", "veterinario: o Responsavel nao recebe o link da agenda",
         "agenda" not in rels(chamar("HAL", "GET", f"/api/veterinarios/{VET}?hateoas=true", token=TK)))
conferir("HAL", "veterinario: o proprio profissional recebe a agenda",
         "agenda" in rels(chamar("HAL", "GET", f"/api/veterinarios/{VET}?hateoas=true", token=VT)))
chamar("vet", "GET", f"/api/veterinarios/{VET}/crmv", token=ADM)
chamar("vet", "POST", f"/api/veterinarios/{VET}/crmv", token=ADM)
chamar("vet", "GET", f"/api/veterinarios/{VET}/agenda", token=VT)
chamar("vet", "GET", f"/api/veterinarios/{VET}/agenda", token=ADM)
chamar("vet", "GET", f"/api/veterinarios/{VET}/agenda", token=TK, esperado=403)      # RN-105
chamar("vet", "GET", f"/api/veterinarios/{VET}/agenda", token=VT2, esperado=403)
chamar("vet", "GET", f"/api/veterinarios/{VET}/servicos", token=TK)
hoje = datetime.now().date()
chamar("vet", "GET", f"/api/veterinarios/{VET}/disponibilidade?de={hoje}&ate={hoje + timedelta(days=3)}", token=TK)
chamar("vet", "GET", "/api/veterinarios/me/extrato", token=VT)
chamar("vet", "PUT", f"/api/veterinarios/{VET2}", token=ADM, esperado=[200, 204], corpo={
    "nome": "Paulo Externo Souza", "crmv": f"{10000 + (MARCA + 7) % 89999}-SP", "ufAtuacao": "SP",
    "email": EMAIL_VET2, "persona": "Autonomo", "plano": "Basico"})

chamar("emp", "GET", "/api/empresas", token=TK)
chamar("emp", "GET", f"/api/empresas/{EMP}", token=ADM)
chamar("emp", "GET", f"/api/empresas/{EMP}/veterinarios", token=ADM)
chamar("emp", "GET", f"/api/empresas/{EMP}/dados-repasse", token=ADM)
chamar("emp", "PUT", f"/api/empresas/{EMP}/dados-repasse", token=ADM, corpo={
    "banco": "341", "agencia": "1234", "conta": "0012345-6", "documentoTitular": "12.345.678/0001-95",
    "chavePix": f"clinica{MARCA}@vetly.com"})
chamar("emp", "PUT", f"/api/empresas/{EMP}", token=ADM, esperado=[200, 204], corpo={
    "nome": f"Clinica Validacao {MARCA} Ltda", "tipo": "Clinica", "administradorId": GESTORA,
    "plano": "Enterprise"})

# Consulta do vinculado redistribuida pela unidade
SRV_V = agenda_e_servicos(VINC, VT3, valor=180.0)
C5, _, _ = consulta_paga(TK, TID, ANI, VINC, SRV_V, pular=8)
chamar("red", "GET", f"/api/consultas/{C5}/redistribuicao/candidatos", token=GT)
chamar("red", "POST", f"/api/consultas/{C5}/redistribuir", token=GT, corpo={
    "novoVeterinarioId": VET, "novoSlotId": slots_livres(VET, TK, 40)[0],
    "motivo": "Veterinario afastado por doenca."})
chamar("red", "POST", f"/api/consultas/{C5}/no-show", token=VT3, esperado=403)        # ja nao e dele
chamar("red", "POST", f"/api/consultas/{C5}/no-show", token=VT, esperado=[200, 204, 422])

# ═════════════════════════════════════════════════════════════════════════════
secao("Consultas, pagamentos e financeiro")

chamar("con", "GET", f"/api/consultas/{CON}?hateoas=true", token=TK)
chamar("con", "GET", "/api/consultas?ordenarPor=dataHora&direcao=desc&tamanho=5", token=TK)
chamar("con", "GET", f"/api/consultas/animal/{ANI}", token=TK)
chamar("con", "GET", f"/api/consultas/veterinario/{VET}", token=VT)
chamar("con", "POST", "/api/consultas", token=ADM, esperado=422, corpo={      # pagamento ja vinculado
    "dataHora": (datetime.now(timezone.utc) + timedelta(days=9)).isoformat(), "modalidade": "Presencial",
    "veterinarioId": VET, "animalId": ANI, "tutorId": TID, "pagamentoId": PAGID})

chamar("pag", "POST", "/api/pagamentos", token=TK, esperado=422, idem=True, corpo={         # RN-006
    "tutorId": TID, "valor": 1.0, "meioPagamento": "Pix"})
chamar("pag", "POST", "/api/pagamentos", token=TK, esperado=422, idem=True, corpo={
    "tutorId": TID, "internacaoId": str(uuid.uuid4()), "valor": 1.0, "meioPagamento": "Pix"})
chamar("pag", "GET", "/api/pagamentos?status=Confirmado&ordenarPor=criadoEm&direcao=desc", token=TK)
chamar("pag", "GET", f"/api/pagamentos/{PAGID}?hateoas=true", token=TK)
chamar("pag", "GET", f"/api/pagamentos/{PAGID}/status", token=TK)
chamar("pag", "POST", f"/api/pagamentos/{PAGID}/processar-split", token=ADM)
chamar("fin", "POST", "/api/financeiro/liquidar", token=ADM, corpo={
    "destinatarioId": VET, "inicio": (INICIO - timedelta(days=1)).isoformat(),
    "fim": (datetime.now(timezone.utc) + timedelta(days=1)).isoformat(), "referencia": f"LIQ-{MARCA}"})
chamar("fin", "GET", "/api/analytics/plataforma", token=ADM)
chamar("fin", "GET", "/api/analytics/plataforma", token=TK, esperado=403)

# ═════════════════════════════════════════════════════════════════════════════
secao("Fidelidade e marketplace")

chamar("fid", "GET", "/api/fidelidade/extrato", token=TK)
cupons = itens(j(chamar("fid", "GET", "/api/fidelidade/cupons", token=TK)))
catalogo = itens(j(chamar("mkt", "GET", "/api/marketplace/itens", token=TK)))
chamar("mkt", "GET", "/api/marketplace/itens?categoria=Alimentacao&ordenarPor=custoEmPontos&direcao=asc&hateoas=true",
       token=TK)
ITEM = catalogo[0]["id"] if catalogo else str(uuid.uuid4())
chamar("mkt", "GET", f"/api/marketplace/itens/{ITEM}", token=TK)
chamar("mkt", "POST", "/api/fidelidade/resgates/simular", token=TK, esperado=[200, 422], corpo={"itemId": ITEM})
chamar("mkt", "POST", f"/api/marketplace/itens/{ITEM}/resgatar", token=TK, esperado=[201, 422], idem=True)
chamar("mkt", "GET", "/api/marketplace/taxas-de-listagem", token=ADM)
ITEM_NOVO = j(chamar("mkt", "POST", "/api/marketplace/itens", token=ADM, esperado=201, corpo={
    "nome": f"Item validacao {MARCA}", "descricao": "Item temporario.", "categoria": "Higiene",
    "fornecedor": "Fornecedor teste", "custoEmPontos": 100, "valorDeReferencia": 10.0, "estoque": 5})).get("id")
chamar("mkt", "POST", "/api/marketplace/itens", token=TK, esperado=403, corpo={
    "nome": "x", "categoria": "Higiene", "fornecedor": "x", "custoEmPontos": 1, "valorDeReferencia": 1})
chamar("mkt", "PUT", f"/api/marketplace/itens/{ITEM_NOVO}", token=ADM, esperado=[200, 204], corpo={
    "ativo": True, "nome": f"Item validacao {MARCA} v2", "categoria": "Higiene", "fornecedor": "Fornecedor teste",
    "custoEmPontos": 120, "valorDeReferencia": 12.0, "estoque": 5})
chamar("mkt", "POST", "/api/marketplace/cupons/validar", token=VT, esperado=400, corpo={"codigoQr": "X"})
chamar("mkt", "POST", "/api/marketplace/cupons/validar", token=VT, esperado=404, idem=True,
       corpo={"codigoQr": "CODIGO-INEXISTENTE"})
if QR:
    chamar("mkt", "POST", "/api/marketplace/cupons/validar", token=TK, esperado=403, idem=True, corpo={"codigoQr": QR})
    chave_qr = str(uuid.uuid4())
    chamar("mkt", "POST", "/api/marketplace/cupons/validar", token=VT, idem=chave_qr, corpo={"codigoQr": QR.lower()})
    chamar("mkt", "POST", "/api/marketplace/cupons/validar", token=VT, idem=chave_qr, corpo={"codigoQr": QR})
    chamar("mkt", "POST", "/api/marketplace/cupons/validar", token=VT, esperado=409, idem=True, corpo={"codigoQr": QR})
if cupons:
    chamar("fid", "GET", f"/api/fidelidade/cupons/{cupons[0]['id']}", token=TK)
else:
    chamar("fid", "GET", f"/api/fidelidade/cupons/{uuid.uuid4()}", token=TK, esperado=404)

# ═════════════════════════════════════════════════════════════════════════════
secao("Notificacoes, lista de espera e lembretes")

chamar("not", "GET", "/api/notificacoes/preferencias", token=TK)
chamar("not", "PUT", "/api/notificacoes/preferencias", token=TK, corpo={"aceitaPromocoes": True})
caixa = itens(j(chamar("not", "GET", f"/api/notificacoes/tutor/{TID}?apenasNaoLidas=true", token=TK)))
if caixa:
    chamar("not", "POST", f"/api/notificacoes/{caixa[0]['id']}/lida", token=TK)
chamar("esp", "GET", "/api/lista-espera", token=TK)
ESP = j(chamar("esp", "POST", "/api/lista-espera", token=TK, esperado=[201, 409], corpo={
    "animalId": ANI, "veterinarioId": VET, "necessidade": "ConsultaRotina"})).get("id")
chamar("esp", "POST", f"/api/lista-espera/{ESP}/confirmar", token=TK, esperado=[200, 201, 422], idem=True,
       corpo={"servicoId": SRV})
ESP2 = j(chamar("esp", "POST", "/api/lista-espera", token=TK, esperado=[201, 409], corpo={
    "animalId": ANI, "veterinarioId": VET2, "necessidade": "ConsultaRotina"})).get("id")
chamar("esp", "DELETE", f"/api/lista-espera/{ESP2}", token=TK, esperado=[200, 204])
LEM = j(chamar("lem", "POST", "/api/lembretes", token=VT, esperado=[200, 201], corpo={
    "animalId": ANI, "tutorId": TID, "tipo": "Retorno",
    "dataEvento": (datetime.now(timezone.utc) + timedelta(days=5)).isoformat()})).get("id")
chamar("lem", "POST", f"/api/lembretes/{LEM}/tentativa", token=VT, esperado=[200, 204])
chamar("lem", "POST", f"/api/lembretes/{LEM}/resposta", token=TK, esperado=[200, 204])

# ═════════════════════════════════════════════════════════════════════════════
secao("Avaliacoes, exames e internacoes")

chamar("ava", "GET", "/api/avaliacoes/pendentes", token=TK)
chamar("ava", "POST", f"/api/avaliacoes/{AVA}/moderar", token=ADM, esperado=[200, 204], corpo={"motivo": "Revisao de rotina"})
chamar("exa", "GET", "/api/exames", token=VT)
EXA = j(chamar("exa", "POST", "/api/exames", token=VT, esperado=201, corpo={
    "animalId": ANI, "veterinarioId": VET, "tipoSolicitacao": "Hemograma completo"})).get("id")
chamar("exa", "GET", f"/api/exames/{EXA}", token=VT)
chamar("exa", "PUT", f"/api/exames/{EXA}/resultado", token=VT, esperado=[200, 204],
       corpo={"resultado": "Leucocitose discreta.", "midiaIds": []})
chamar("exa", "PUT", f"/api/exames/{EXA}/liberar", token=VT, esperado=[200, 204])

chamar("int", "GET", "/api/internacoes", token=VT)
INT = j(chamar("int", "POST", "/api/internacoes", token=VT, esperado=201, corpo={
    "animalId": ANI, "veterinarioId": VET, "valorCaucao": 500.0, "meioPagamento": "Pix"})).get("id")
chamar("int", "GET", f"/api/internacoes/{INT}", token=VT)
chamar("int", "PUT", f"/api/internacoes/{INT}/procedimentos", token=VT, esperado=[200, 204], corpo={
    "procedimentos": [{"procedimento": "Fluidoterapia", "data": datetime.now(timezone.utc).isoformat(), "valor": 150.0}]})
chamar("int", "POST", f"/api/internacoes/{INT}/alta", token=VT)
caucao = next((p for p in itens(j(requests.get(f"{BASE}/api/pagamentos?tamanho=50",
                                               headers={"Authorization": f"Bearer {TK}"}, timeout=60)))
               if p.get("internacaoId") == INT), {})
conferir("int", "caucao da internacao sai com o split do plano",
         (caucao.get("repasse") or 0) > 0
         and (caucao.get("comissao") or 0) + (caucao.get("repasse") or 0) == caucao.get("valor"),
         f"valor={caucao.get('valor')} comissao={caucao.get('comissao')} repasse={caucao.get('repasse')}")

# ═════════════════════════════════════════════════════════════════════════════
secao("Rotas internas e IA")

chamar("int", "POST", "/api/internos/stt/callback", esperado=401, corpo={"segmentoId": str(uuid.uuid4()), "status": "Ok"})
chamar("int", "POST", "/api/internos/stt/callback", cab={"X-Vetly-Service-Token": TOKEN_INTERNO},
       esperado=404, corpo={"segmentoId": str(uuid.uuid4()), "status": "Ok", "texto": "x"})

if not RAPIDO:
    chamar("ia", "POST", "/api/ia/triagem", token=TK, timeout=600,
           corpo={"especie": "Canino", "sintomas": ["vomito"], "idadeAnos": 3})
    chamar("ia", "POST", "/api/ia/diagnostico", token=VT, timeout=600, corpo={
        "especie": "Canino", "raca": "Golden", "idadeAnos": 3, "pesoKg": 31.5, "sintomas": ["vomito", "apatia"]})
    chamar("ia", "POST", "/api/ia/protocolo", token=VT, timeout=600,
           corpo={"diagnostico": "Gastrite", "especie": "Canino", "pesoKg": 31.5})
    chamar("ia", "POST", "/api/ia/protocolo", token=VT, esperado=422,
           corpo={"diagnostico": "Gastrite", "especie": "Canino", "pesoKg": 0})
    chamar("ia", "POST", "/api/ia/orientacoes", token=TK, timeout=600, corpo={
        "especie": "Canino", "diagnostico": "Gastrite", "medicamentos": ["Omeprazol"], "conduta": "Dieta branda"})
    chamar("ia", "POST", "/api/ia/estruturar", token=VT, timeout=600, corpo={
        "transcricao": "Canino de 3 anos, vomito ha dois dias. Mucosas normais. Prescrevo dieta branda.",
        "especie": "Canino", "idadeAnos": 3, "pesoKg": 31.5})

# ═════════════════════════════════════════════════════════════════════════════
secao("Fechamento")

# 7.10 de novo, so com o dinheiro desta rodada: comissao + repasse + desconto = bruto
fim = (datetime.now(timezone.utc) + timedelta(minutes=1)).replace(microsecond=0)
c = j(chamar("7.10", "GET", f"/api/financeiro/consolidado?inicio={INICIO.isoformat().replace('+00:00', 'Z')}"
                            f"&fim={fim.isoformat().replace('+00:00', 'Z')}", token=ADM))
conferir("7.10", "o consolidado da rodada fecha", c.get("fecha") is True,
         f"bruto={c.get('valorBruto')} comissao={c.get('comissaoLiquida')} repasse={c.get('repasseTotal')}")

chamar("ses", "POST", "/api/auth/logout", esperado=[200, 204], corpo={"refreshToken": novo})
chamar("del", "DELETE", f"/api/animais/{ANI_DEL}", token=TK, esperado=[200, 204])
chamar("del", "DELETE", f"/api/marketplace/itens/{ITEM_NOVO}", token=ADM, esperado=[200, 204])
chamar("del", "DELETE", f"/api/tutores/{TUT_BALCAO}", token=ADM, esperado=[200, 204])
EMP_DEL = j(chamar("del", "POST", "/api/empresas", token=ADM, esperado=201, corpo={
    "nome": f"Empresa descartavel {MARCA}", "tipo": "PetShop", "administradorId": GESTORA})).get("id")
chamar("del", "DELETE", f"/api/empresas/{EMP_DEL}", token=ADM, esperado=[200, 204])
chamar("del", "DELETE", f"/api/veterinarios/{VET2}", token=ADM, esperado=[200, 204])

metricas = requests.get(BASE + "/metrics", timeout=60).text
conferir("8", "metrica de requisicoes exposta", "vetly_http_requisicoes_total" in metricas)

# ═════════════════════════════════════════════════════════════════════════════
cobertos = {(x[1], x[2]) for x in R}
ignoradas = {("POST", "/api/consultas/{id}/captura/segmentos"), ("GET", "/api/consultas/{id}/rascunho"),
             ("PUT", "/api/consultas/{id}/validar-diagnostico")} if RAPIDO else set()
faltando = [(m, p) for p, m in TEMPLATES
            if (m, p) not in cobertos and (m, p) not in ignoradas and not (RAPIDO and p.startswith("/api/ia/"))]
falhas = [x for x in R if not x[6]]

print("\n" + "=" * 78)
print(f"ambiente: {'Development' if DEV else 'Production'}   chamadas e conferencias: {len(R)}   "
      f"operacoes do OpenAPI cobertas: {len(TEMPLATES) - len(faltando)}/{len(TEMPLATES)}"
      f"{'  (--rapido: IA fora)' if RAPIDO else ''}")
for m, p in faltando:
    print(f"  sem passo no validador: {m} {p}")

if falhas:
    print(f"\n{len(falhas)} FALHA(S):")
    for passo, verbo, _, caminho, esp, obt, _, det in falhas:
        print(f"  {passo:5} {verbo:6} {caminho[:60]:60} esperado {'/'.join(map(str, esp))}, obtido {obt}  {det[:80]}")
    sys.exit(1)

print("Tudo verde.")
