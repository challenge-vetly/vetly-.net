#!/usr/bin/env python3
"""
Percorre a API inteira contra um ambiente de verdade e diz o que responde o que.

Nao substitui a suite de testes: a suite prova regra de negocio isolada, e este
script prova que o conjunto sobe, se autentica, encadeia e responde pelo caminho que
o app do Responsavel e o do veterinario percorrem de fato.

O harness monta estado de verdade -- Admin, veterinario, Responsavel, animal, consulta,
pagamento, documento -- porque a maioria dos endpoints so e alcancavel depois de outro
endpoint ter respondido. Testar isoladamente devolveria 404 em quase tudo e nao provaria
nada.

Uso:
    python deploy/validar-endpoints.py https://host [--rapido]

    --rapido pula as rotas de IA, que levam de 30 a 90 segundos cada em CPU.
"""

import json
import sys
import time
import uuid
from datetime import datetime, timedelta, timezone

import requests

BASE = sys.argv[1].rstrip("/") if len(sys.argv) > 1 else "http://localhost:5140"
RAPIDO = "--rapido" in sys.argv

# Resultados: (verbo, rota, esperado, obtido, veredito, detalhe)
resultados = []
estado = {}


def registrar(verbo, rota, esperado, obtido, detalhe=""):
    """Compara o status obtido com o conjunto de status aceitaveis."""
    aceitos = esperado if isinstance(esperado, (list, tuple, set)) else [esperado]
    ok = obtido in aceitos
    resultados.append((verbo, rota, "/".join(str(e) for e in aceitos), obtido, ok, detalhe))
    marca = "ok  " if ok else "FALHA"
    print(f"  [{marca}] {verbo:6} {rota:58} {obtido}  {detalhe[:60]}")
    return ok


def chamar(verbo, rota, token=None, corpo=None, esperado=200, idem=False, timeout=120):
    """Faz a chamada, registra o desfecho e devolve a resposta."""
    cab = {"Content-Type": "application/json"}
    if token:
        cab["Authorization"] = f"Bearer {token}"
    if idem:
        cab["Idempotency-Key"] = str(uuid.uuid4())

    try:
        r = requests.request(
            verbo, BASE + rota, headers=cab,
            data=json.dumps(corpo) if corpo is not None else None,
            timeout=timeout)
    except Exception as e:                                    # noqa: BLE001
        registrar(verbo, rota, esperado, 0, f"EXCECAO {type(e).__name__}")
        return None

    detalhe = ""
    if r.status_code >= 400:
        try:
            detalhe = str(r.json().get("detail") or r.json().get("title") or "")[:70]
        except Exception:                                     # noqa: BLE001
            detalhe = r.text[:70]

    registrar(verbo, rota, esperado, r.status_code, detalhe)
    return r


def corpo(r):
    """JSON da resposta, ou None."""
    if r is None or not r.ok:
        return None
    try:
        return r.json()
    except Exception:                                         # noqa: BLE001
        return None


def secao(titulo):
    print(f"\n=== {titulo}")


# ─────────────────────────────────────────────────────────────────────────────
secao("Infraestrutura e observabilidade")

for rota in ["/health", "/health/live", "/health/ready", "/metrics", "/openapi/v1.json"]:
    chamar("GET", rota, esperado=200)

saude = corpo(requests.get(BASE + "/health/ready", timeout=60))
if saude:
    for c in saude.get("checks", []):
        print(f"        {c['nome']}: {c['status']}")

# ─────────────────────────────────────────────────────────────────────────────
secao("Auth")

marca = int(time.time())
email_tutor = f"val-tutor-{marca}@exemplo.com"

r = chamar("POST", "/api/auth/registro/tutor", corpo={
    "nome": "Ana Validacao", "email": email_tutor,
    "telefone": "11999998888", "senha": "senha-forte-123"}, esperado=201)
d = corpo(r) or {}
estado["tutor_token"] = d.get("token")
estado["tutor_id"] = d.get("tutorId")
estado["tutor_refresh"] = d.get("refreshToken")

chamar("POST", "/api/auth/registro/tutor", corpo={
    "nome": "Duplicado", "email": email_tutor,
    "telefone": "11999998888", "senha": "senha-forte-123"}, esperado=422)

chamar("POST", "/api/auth/login", corpo={"email": email_tutor, "senha": "errada"}, esperado=422)
chamar("POST", "/api/auth/login", corpo={"email": email_tutor, "senha": "senha-forte-123"}, esperado=200)
chamar("GET", "/api/auth/me", token=estado["tutor_token"], esperado=200)
chamar("POST", "/api/auth/token", corpo={"usuario": "x", "role": "Admin"}, esperado=404)

# Admin do bootstrap
senha_admin = estado.get("senha_admin") or "__nao_informada__"
if len(sys.argv) > 2 and sys.argv[2] not in ("--rapido",):
    senha_admin = sys.argv[2]

r = chamar("POST", "/api/auth/login",
           corpo={"email": "admin@vetly.com.br", "senha": senha_admin},
           esperado=[200, 422])
d = corpo(r) or {}
estado["admin_token"] = d.get("token")
if d.get("role"):
    print(f"        role do admin: {d['role']}")

# ─────────────────────────────────────────────────────────────────────────────
secao("Consentimento LGPD (RN-060)")

tid, tk = estado["tutor_id"], estado["tutor_token"]

chamar("GET", "/api/animais", token=tk, esperado=422)          # sem consentimento
chamar("GET", f"/api/tutores/{tid}/consentimentos", token=tk, esperado=200)
chamar("PUT", f"/api/tutores/{tid}/consentimentos", token=tk, esperado=200, corpo={
    "consentimentos": [
        {"finalidade": "Atendimento", "concedido": True},
        {"finalidade": "Lembretes", "concedido": True},
        {"finalidade": "Compartilhamento", "concedido": True},
        {"finalidade": "Promocoes", "concedido": True}]})
chamar("GET", "/api/animais", token=tk, esperado=200)          # agora passa

# ─────────────────────────────────────────────────────────────────────────────
secao("Tutores")

chamar("GET", f"/api/tutores/{tid}", token=tk, esperado=200)
chamar("GET", f"/api/tutores/{tid}/animais", token=tk, esperado=200)
chamar("GET", f"/api/tutores/{tid}/carteira", token=tk, esperado=200)
chamar("GET", "/api/tutores", token=tk, esperado=403)          # so Admin
chamar("PUT", f"/api/tutores/{tid}", token=tk, esperado=[200, 204], corpo={
    "nome": "Ana Validacao Silva", "email": email_tutor, "telefone": "11988887777"})

r = chamar("POST", f"/api/tutores/{tid}/dispositivos", token=tk, esperado=[200, 201], corpo={
    "pushToken": f"token-{marca}", "plataforma": "Android", "modelo": "Pixel"})
disp = (corpo(r) or {}).get("id")
chamar("GET", f"/api/tutores/{tid}/dispositivos", token=tk, esperado=200)
if disp:
    chamar("DELETE", f"/api/tutores/{tid}/dispositivos/{disp}", token=tk, esperado=[200, 204])

# ─────────────────────────────────────────────────────────────────────────────
secao("Animais")

r = chamar("POST", "/api/animais", token=tk, esperado=201, corpo={
    "nome": "Thor", "especie": "Canino", "raca": "Golden",
    "dataNascimento": "2022-03-01T00:00:00Z", "tutorId": tid,
    "pesoKg": 28.5, "sexo": "Macho", "castrado": True})
animal = (corpo(r) or {}).get("id")
estado["animal"] = animal

if animal:
    for rota, esp in [
            (f"/api/animais/{animal}", 200),
            (f"/api/animais/{animal}/board", 200),
            (f"/api/animais/{animal}/obrigacoes", 200),
            (f"/api/animais/{animal}/acessos", 200),
            (f"/api/animais/{animal}/prontuarios", 200),
            (f"/api/animais/{animal}/exames", 200)]:
        chamar("GET", rota, token=tk, esperado=esp)

    chamar("PUT", f"/api/animais/{animal}/peso", token=tk, esperado=[200, 204],
           corpo={"pesoKg": 29.0})
    chamar("PUT", f"/api/animais/{animal}", token=tk, esperado=[200, 204], corpo={
        "nome": "Thor", "especie": "Canino", "raca": "Golden Retriever",
        "dataNascimento": "2022-03-01T00:00:00Z", "tutorId": tid, "pesoKg": 29.0})

# ─────────────────────────────────────────────────────────────────────────────
secao("Obrigacoes do pet")

if animal:
    r = chamar("POST", f"/api/obrigacoes/animal/{animal}", token=tk, esperado=[200, 201], corpo={
        "tipo": "Vacina", "descricao": "Antirrabica",
        "proximoVencimento": (datetime.now(timezone.utc) + timedelta(days=20)).isoformat(),
        "periodicidadeEmDias": 365})
    obr = (corpo(r) or {}).get("id")
    chamar("GET", f"/api/obrigacoes/animal/{animal}", token=tk, esperado=200)
    chamar("POST", f"/api/obrigacoes/animal/{animal}/derivar-da-carteira", token=tk,
           esperado=[200, 201])
    if obr:
        chamar("POST", f"/api/obrigacoes/{obr}/cumprir", token=tk, esperado=[200, 204],
               corpo={"quando": datetime.now(timezone.utc).isoformat()})
        chamar("DELETE", f"/api/obrigacoes/{obr}", token=tk, esperado=[200, 204])

# ─────────────────────────────────────────────────────────────────────────────
secao("Veterinarios e empresas (Admin)")

adm = estado.get("admin_token")

if adm:
    email_vet = f"val-vet-{marca}@exemplo.com"
    crmv = f"{(marca % 90000 + 10000) // 10 * 10 + 5}-SP"

    r = chamar("POST", "/api/veterinarios", token=adm, esperado=201, corpo={
        "nome": "Dra. Marina Validacao", "crmv": crmv, "ufAtuacao": "SP",
        "email": email_vet, "persona": "Autonomo", "plano": "Profissional"})
    d = corpo(r) or {}
    vet_id = (d.get("veterinario") or {}).get("id")
    senha_vet = d.get("senhaTemporaria")
    estado["vet_id"] = vet_id

    if vet_id and senha_vet:
        r = chamar("POST", "/api/auth/login",
                   corpo={"email": email_vet, "senha": senha_vet}, esperado=200)
        estado["vet_token"] = (corpo(r) or {}).get("token")

    chamar("GET", "/api/veterinarios", token=tk, esperado=200)
    chamar("GET", "/api/veterinarios/regiao/SP", token=tk, esperado=200)
    if vet_id:
        chamar("GET", f"/api/veterinarios/{vet_id}", token=tk, esperado=200)
        chamar("GET", f"/api/veterinarios/{vet_id}/crmv", token=adm, esperado=200)
        chamar("POST", f"/api/veterinarios/{vet_id}/crmv", token=adm, esperado=200)
        chamar("GET", f"/api/veterinarios/{vet_id}/agenda", token=adm, esperado=200)

    chamar("GET", "/api/empresas", token=tk, esperado=200)
    r = chamar("POST", "/api/empresas", token=adm, esperado=201, corpo={
        "nome": f"Clinica Validacao {marca}", "tipo": "Clinica",
        "administradorId": vet_id, "plano": "Profissional"})
    emp = (corpo(r) or {}).get("id")
    if emp:
        chamar("GET", f"/api/empresas/{emp}", token=adm, esperado=200)
        chamar("GET", f"/api/empresas/{emp}/veterinarios", token=adm, esperado=200)
        chamar("GET", f"/api/empresas/{emp}/dados-repasse", token=adm, esperado=200)
        chamar("PUT", f"/api/empresas/{emp}/dados-repasse", token=adm, esperado=200, corpo={
            "banco": "341", "agencia": "1234", "conta": "0012345-6",
            "documentoTitular": "12.345.678/0001-95", "chavePix": f"clinica{marca}@vetly.com"})

# ─────────────────────────────────────────────────────────────────────────────
secao("Agenda, servicos e repasse (Veterinario)")

vt, vet_id = estado.get("vet_token"), estado.get("vet_id")

if vt and vet_id:
    chamar("GET", "/api/veterinarios/me/dados-repasse", token=vt, esperado=200)
    chamar("PUT", "/api/veterinarios/me/dados-repasse", token=vt, esperado=200, corpo={
        "banco": "341", "agencia": "1234", "conta": "0098765-4",
        "documentoTitular": "123.456.789-09", "chavePix": f"vet{marca}@vetly.com"})
    chamar("PUT", "/api/veterinarios/me/dados-repasse", token=vt, esperado=400, corpo={
        "banco": "341", "agencia": "1", "conta": "1",
        "documentoTitular": "123", "chavePix": "x"})
    chamar("GET", "/api/veterinarios/me/extrato", token=vt, esperado=200)

    chamar("PUT", f"/api/veterinarios/{vet_id}/agenda-config", token=vt, esperado=200, corpo={
        "dias": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"],
        "horaInicio": "08:00", "horaFim": "18:00",
        "duracaoMinutos": 30, "intervaloMinutos": 0})

    cfg = corpo(chamar("GET", f"/api/veterinarios/{vet_id}/agenda-config", token=vt, esperado=200))
    if cfg:
        certo = cfg.get("horaInicio") == "08:00" and cfg.get("horaFim") == "18:00"
        print(f"        horario persistido: {cfg.get('horaInicio')}-{cfg.get('horaFim')} "
              f"{'OK' if certo else '<-- TRUNCADO'}")
        if not certo:
            resultados.append(("GET", "agenda-config (valor)", "08:00-18:00",
                               f"{cfg.get('horaInicio')}-{cfg.get('horaFim')}", False, "truncamento"))

    r = chamar("PUT", f"/api/veterinarios/{vet_id}/servicos", token=vt, esperado=200, corpo={
        "servicos": [
            {"tipo": "ConsultaRotina", "valor": 200.0, "duracaoMinutos": 30, "aceitaPlanoPet": False},
            {"tipo": "Cirurgia", "valor": 1500.0, "duracaoMinutos": 300, "aceitaPlanoPet": False}]})
    servicos = corpo(r) or []
    cir = [s for s in servicos if s.get("tipo") == "Cirurgia"]
    if cir:
        okd = cir[0].get("duracaoMinutos") == 300
        print(f"        duracao da cirurgia: {cir[0].get('duracaoMinutos')} "
              f"{'OK' if okd else '<-- TRUNCADO'}")
        if not okd:
            resultados.append(("PUT", "servicos (duracao)", "300",
                               str(cir[0].get("duracaoMinutos")), False, "truncamento"))

    estado["servico"] = next((s["id"] for s in servicos if s.get("tipo") == "ConsultaRotina"), None)
    chamar("GET", f"/api/veterinarios/{vet_id}/servicos", token=tk, esperado=200)
    chamar("GET", f"/api/veterinarios/{vet_id}/disponibilidade", token=tk, esperado=200)

# ─────────────────────────────────────────────────────────────────────────────
secao("Busca por geolocalizacao")

chamar("GET", "/api/busca?lat=-23.5614&lng=-46.6558&raioKm=25", token=tk, esperado=400)
if estado.get("animal"):
    chamar("GET", f"/api/busca?animalId={estado['animal']}&lat=-23.5614&lng=-46.6558&raioKm=25",
           token=tk, esperado=200)

# ─────────────────────────────────────────────────────────────────────────────
secao("Checkout, pagamento e webhook")

if vt and vet_id and animal and estado.get("servico"):
    disp = corpo(requests.get(
        f"{BASE}/api/veterinarios/{vet_id}/disponibilidade",
        headers={"Authorization": f"Bearer {tk}"}, timeout=60))
    slots = [h["id"] for d in (disp or {}).get("dias", []) for h in d.get("horarios", [])]

    if slots:
        r = chamar("POST", "/api/consultas/checkout", token=tk, esperado=201, idem=True, corpo={
            "animalId": animal, "prestadorId": vet_id,
            "slotId": slots[0], "servicoId": estado["servico"]})
        consulta = (corpo(r) or {}).get("consultaId")
        estado["consulta"] = consulta

        # O mesmo slot, de novo: a concorrencia e resolvida no banco (RN-035)
        chamar("POST", "/api/consultas/checkout", token=tk, esperado=409, idem=True, corpo={
            "animalId": animal, "prestadorId": vet_id,
            "slotId": slots[0], "servicoId": estado["servico"]})

        if consulta:
            r = chamar("POST", "/api/pagamentos", token=tk, esperado=[200, 201, 202], idem=True,
                       corpo={"tutorId": tid, "consultaId": consulta,
                              "valor": 1.0, "meioPagamento": "Pix"})
            pag = corpo(r) or {}
            estado["pagamento"] = pag.get("id")
            cobrado = pag.get("valor")
            print(f"        valor cobrado: {cobrado} (enviei 1.0) "
                  f"{'OK' if cobrado == 200 else '<-- ACEITOU O DO CLIENTE'}")
            if cobrado != 200:
                resultados.append(("POST", "/api/pagamentos (valor)", "200",
                                   str(cobrado), False, "preco do cliente aceito"))

            ref = (pag.get("instrucoes") or {}).get("referenciaExterna")
            estado["ref"] = ref

            chamar("GET", f"/api/pagamentos/{pag.get('id')}", token=tk, esperado=200)
            chamar("GET", f"/api/pagamentos/{pag.get('id')}/status", token=tk, esperado=200)
            chamar("GET", "/api/pagamentos", token=tk, esperado=200)

# webhook exige o token de servico
chamar("POST", "/api/internos/pagamentos/webhook", esperado=401,
       corpo={"referenciaExterna": "x", "status": "Confirmado", "assinado": True})
chamar("POST", "/api/internos/stt/callback", esperado=401,
       corpo={"segmentoId": str(uuid.uuid4()), "status": "Ok"})

# ─────────────────────────────────────────────────────────────────────────────
secao("Consultas")

c = estado.get("consulta")
if c:
    for rota in [f"/api/consultas/{c}", "/api/consultas",
                 f"/api/consultas/animal/{animal}",
                 f"/api/consultas/veterinario/{vet_id}"]:
        chamar("GET", rota, token=tk if "veterinario" not in rota else vt, esperado=200)

    chamar("PUT", f"/api/consultas/{c}/pre-sintomas", token=tk, esperado=[200, 204], corpo={
        "queixaPrincipal": "Vomito ha 2 dias, recusa alimentar",
        "duracaoEmDias": 2, "sinaisObservados": ["vomito", "apatia"],
        "alimentacaoNormal": False, "mudancaDeComportamento": True})
    chamar("GET", f"/api/consultas/{c}/simulacao-cancelamento", token=tk, esperado=200)
    chamar("GET", f"/api/consultas/{c}/briefing", token=vt, esperado=200)

# ─────────────────────────────────────────────────────────────────────────────
secao("Fidelidade")

chamar("GET", "/api/fidelidade/saldo", token=tk, esperado=200)
chamar("GET", "/api/fidelidade/extrato", token=tk, esperado=200)
chamar("GET", "/api/fidelidade/cupons", token=tk, esperado=200)
chamar("POST", "/api/fidelidade/resgates/simular", token=tk, esperado=[200, 422], corpo={
    "itemRef": "racao-premium", "itemNome": "Racao", "categoria": "Alimentacao", "pontos": 100})

# ─────────────────────────────────────────────────────────────────────────────
secao("Notificacoes e lista de espera")

chamar("GET", f"/api/notificacoes/tutor/{tid}", token=tk, esperado=200)
chamar("GET", "/api/notificacoes/preferencias", token=tk, esperado=200)
chamar("PUT", "/api/notificacoes/preferencias", token=tk, esperado=200,
       corpo={"aceitaPromocoes": True})
chamar("GET", "/api/lista-espera", token=tk, esperado=200)

if animal and vet_id:
    r = chamar("POST", "/api/lista-espera", token=tk, esperado=[201, 409], corpo={
        "animalId": animal, "veterinarioId": vet_id, "necessidade": "ConsultaRotina"})
    item = (corpo(r) or {}).get("id")
    if item:
        chamar("DELETE", f"/api/lista-espera/{item}", token=tk, esperado=[200, 204])

# ─────────────────────────────────────────────────────────────────────────────
secao("Midia e storage assinado")

if c:
    r = chamar("POST", "/api/midia/upload-url", token=vt, esperado=[200, 201], corpo={
        "tipo": "AudioConsulta", "contentType": "audio/wav", "consultaId": c})
    up = corpo(r) or {}
    if up.get("uploadUrl"):
        wav = (b"RIFF" + (36 + 32000).to_bytes(4, "little") + b"WAVEfmt " +
               (16).to_bytes(4, "little") + (1).to_bytes(2, "little") +
               (1).to_bytes(2, "little") + (16000).to_bytes(4, "little") +
               (32000).to_bytes(4, "little") + (2).to_bytes(2, "little") +
               (16).to_bytes(2, "little") + b"data" + (32000).to_bytes(4, "little") +
               b"\x00" * 32000)
        rr = requests.put(up["uploadUrl"], data=wav,
                          headers={"Content-Type": "audio/wav"}, timeout=120)
        registrar("PUT", "/api/storage/{chave} (URL assinada)", 204, rr.status_code)
        estado["midia"] = up.get("midiaId")
        chamar("GET", f"/api/midia/{up.get('midiaId')}/url", token=vt, esperado=200)

# ─────────────────────────────────────────────────────────────────────────────
secao("Dashboard, financeiro e analytics")

if vt:
    chamar("GET", "/api/dashboard/veterinario", token=vt, esperado=200)
    chamar("GET", "/api/dashboard/unidade", token=vt, esperado=403)
if adm:
    chamar("GET", "/api/dashboard/unidade", token=adm, esperado=200)
    chamar("GET", "/api/financeiro/consolidado", token=adm, esperado=200)
    chamar("GET", "/api/analytics/plataforma", token=adm, esperado=200)
chamar("GET", "/api/analytics/plataforma", token=tk, esperado=403)
chamar("GET", "/api/financeiro/consolidado", token=tk, esperado=403)

# ─────────────────────────────────────────────────────────────────────────────
secao("Avaliacoes, exames, internacoes, colmeia, lembretes")

chamar("GET", "/api/avaliacoes/pendentes", token=tk, esperado=200)
if vet_id:
    chamar("GET", f"/api/avaliacoes/veterinario/{vet_id}", token=tk, esperado=200)

chamar("GET", "/api/exames", token=vt or tk, esperado=200)
chamar("GET", "/api/internacoes", token=vt or tk, esperado=200)
if animal:
    chamar("GET", f"/api/colmeia/animal/{animal}", token=tk, esperado=200)
    chamar("GET", f"/api/colmeia/animal/{animal}/acessos", token=tk, esperado=200)

# ─────────────────────────────────────────────────────────────────────────────
secao("Isolamento entre Responsaveis (RN-105)")

r = requests.post(f"{BASE}/api/auth/registro/tutor", json={
    "nome": "Intruso", "email": f"intruso-{marca}@exemplo.com",
    "telefone": "11999990000", "senha": "senha-forte-123"}, timeout=60)
d_int = corpo(r) or {}
intruso, intruso_id = d_int.get("token"), d_int.get("tutorId")

# O intruso PRECISA consentir: sem isso o filtro de LGPD responde 422 antes de a posse
# por linha ser avaliada, e o teste provaria o portao de consentimento, nao a RN-105.
if intruso and intruso_id:
    requests.put(f"{BASE}/api/tutores/{intruso_id}/consentimentos",
                 headers={"Authorization": f"Bearer {intruso}",
                          "Content-Type": "application/json"},
                 json={"consentimentos": [{"finalidade": "Atendimento", "concedido": True}]},
                 timeout=60)

if intruso and animal:
    chamar("GET", f"/api/animais/{animal}", token=intruso, esperado=403)
    chamar("GET", f"/api/tutores/{tid}/carteira", token=intruso, esperado=403)
    chamar("GET", f"/api/animais/{animal}/board", token=intruso, esperado=403)
    if estado.get("consulta"):
        chamar("GET", f"/api/consultas/{estado['consulta']}", token=intruso, esperado=403)


# -----------------------------------------------------------------------------
secao("Ciclo clinico completo: webhook -> captura -> IA -> documento -> avaliacao")

import os
token_servico = os.environ.get("VETLY_TOKEN_INTERNO")

if token_servico and estado.get("ref"):
    rr = requests.post(f"{BASE}/api/internos/pagamentos/webhook",
                       headers={"X-Vetly-Service-Token": token_servico,
                                "Content-Type": "application/json"},
                       json={"referenciaExterna": estado["ref"],
                             "status": "Confirmado", "assinado": True}, timeout=120)
    registrar("POST", "/api/internos/pagamentos/webhook (confirmado)", 200, rr.status_code)
    dd = corpo(rr) or {}
    print(f"        pagamento={dd.get('statusPagamento')} consulta={dd.get('statusConsulta')}")

c = estado.get("consulta")

if c and vt:
    chamar("POST", f"/api/consultas/{c}/iniciar", token=vt, esperado=200, corpo={})
    chamar("POST", f"/api/consultas/{c}/iniciar", token=vt, esperado=409, corpo={})
    chamar("GET", f"/api/consultas/{c}/captura", token=vt, esperado=200)

    if estado.get("midia"):
        chamar("POST", f"/api/consultas/{c}/captura/segmentos", token=vt, esperado=202, corpo={
            "sequencia": 1, "midiaId": estado["midia"],
            "duracaoMs": 2000, "inicioRelativoMs": 0})
        chamar("POST", f"/api/consultas/{c}/captura/segmentos", token=vt, esperado=409, corpo={
            "sequencia": 1, "midiaId": estado["midia"],
            "duracaoMs": 2000, "inicioRelativoMs": 0})

    chamar("POST", f"/api/consultas/{c}/encerrar", token=vt, esperado=200, corpo={})

    pronto = False
    for _ in range(40):
        time.sleep(15)
        rr = requests.get(f"{BASE}/api/consultas/{c}/rascunho",
                          headers={"Authorization": f"Bearer {vt}"}, timeout=60)
        if rr.status_code == 200:
            pronto = True
            break
    registrar("GET", "/api/consultas/{id}/rascunho", 200, 200 if pronto else 408,
              "" if pronto else "rascunho nao ficou pronto")

    chamar("PUT", f"/api/consultas/{c}/validar-diagnostico", token=vt, esperado=200, corpo={
        "decisao": "Aprovado",
        "anamnese": "Vomito ha 2 dias, recusa alimentar.",
        "exameFisico": "Mucosas normocoradas, TPC 2s.",
        "hipotesesDiagnosticas": ["Gastroenterite aguda"],
        "conduta": "Metronidazol 15mg/kg BID por 5 dias.",
        "orientacoes": "Retorno em 5 dias."})

    chamar("GET", f"/api/consultas/{c}/auditoria-ia", token=vt, esperado=200)

    r = chamar("POST", f"/api/documentos/consulta/{c}?tipo=Prontuario", token=vt, esperado=201)
    doc = (corpo(r) or {}).get("id")

    r2 = chamar("POST", f"/api/documentos/consulta/{c}?tipo=ReceitaVeterinaria",
                token=vt, esperado=[201, 422])
    receita = (corpo(r2) or {}).get("id")

    chamar("GET", f"/api/documentos/consulta/{c}", token=vt, esperado=200)

    if doc:
        chamar("GET", f"/api/documentos/{doc}", token=vt, esperado=200)
        chamar("POST", f"/api/documentos/{doc}/assinar", token=vt, esperado=200,
               corpo={"nomeCompleto": "Dra. Marina Validacao"})
        chamar("POST", f"/api/documentos/{doc}/publicar", token=vt, esperado=200)
        chamar("POST", f"/api/documentos/{doc}/lido", token=tk, esperado=[200, 204])
        chamar("POST", f"/api/documentos/{doc}/correcao", token=vt, esperado=[200, 201], corpo={
            "conteudo": "Correcao: dose ajustada para 12mg/kg.",
            "justificativa": "Ajuste de posologia pelo peso aferido."})

    if receita:
        chamar("POST", f"/api/documentos/{receita}/assinar", token=vt, esperado=200,
               corpo={"nomeCompleto": "Dra. Marina Validacao"})

    if animal:
        chamar("GET", f"/api/documentos/animal/{animal}", token=tk, esperado=200)

    chamar("POST", f"/api/consultas/{c}/finalizar", token=vt, esperado=200, corpo={})

    r = chamar("POST", f"/api/avaliacoes/consulta/{c}", token=tk, esperado=[200, 201], corpo={
        "nota": 5, "comentario": "Atendimento excelente."})
    aval = (corpo(r) or {}).get("id")

    if aval:
        det = corpo(chamar("GET", f"/api/avaliacoes/veterinario/{vet_id}", token=tk, esperado=200))
        notas = [a.get("nota") for a in det] if isinstance(det, list) else []
        if notas:
            marca_ok = "OK" if 5 in notas else "<-- COLAPSADA"
            print(f"        notas gravadas: {notas} {marca_ok}")
            if 5 not in notas:
                resultados.append(("GET", "avaliacoes (nota)", "5", str(notas), False, "colapsada"))
        chamar("POST", f"/api/avaliacoes/{aval}/resposta", token=vt, esperado=[200, 204],
               corpo={"resposta": "Obrigada pela confianca!"})
        if adm:
            chamar("POST", f"/api/avaliacoes/{aval}/moderar", token=adm, esperado=[200, 204],
                   corpo={"motivo": "Revisao de rotina"})

    disp2 = corpo(requests.get(f"{BASE}/api/veterinarios/{vet_id}/disponibilidade",
                               headers={"Authorization": f"Bearer {tk}"}, timeout=60))
    livres = [h["id"] for d in (disp2 or {}).get("dias", []) for h in d.get("horarios", [])]
    if livres:
        chamar("POST", f"/api/consultas/{c}/retorno", token=vt, esperado=[200, 201], idem=True,
               corpo={"slotId": livres[0], "motivo": "Reavaliacao apos 5 dias."})

# -----------------------------------------------------------------------------
secao("Exames e internacoes")

if vt and animal and vet_id:
    r = chamar("POST", "/api/exames", token=vt, esperado=201, corpo={
        "animalId": animal, "veterinarioId": vet_id,
        "tipo": "Hemograma", "observacoes": "Jejum de 8h."})
    exame = (corpo(r) or {}).get("id")
    if exame:
        chamar("GET", f"/api/exames/{exame}", token=vt, esperado=200)
        chamar("PUT", f"/api/exames/{exame}/resultado", token=vt, esperado=[200, 204], corpo={
            "resultado": "Leucocitose discreta.", "midiaIds": []})
        chamar("PUT", f"/api/exames/{exame}/liberar", token=vt, esperado=[200, 204])

    r = chamar("POST", "/api/internacoes", token=vt, esperado=201, corpo={
        "animalId": animal, "veterinarioId": vet_id,
        "motivo": "Desidratacao severa", "valorCaucao": 500.0})
    intern = (corpo(r) or {}).get("id")
    if intern:
        chamar("GET", f"/api/internacoes/{intern}", token=vt, esperado=200)
        chamar("PUT", f"/api/internacoes/{intern}/procedimentos", token=vt, esperado=[200, 204],
               corpo={"procedimentos": [
                   {"descricao": "Fluidoterapia",
                    "data": datetime.now(timezone.utc).isoformat(), "valor": 150.0}]})
        chamar("POST", f"/api/internacoes/{intern}/alta", token=vt, esperado=200,
               corpo={"resumoAlta": "Alta com melhora clinica.", "diarias": 2})

# -----------------------------------------------------------------------------
secao("Colmeia e lembretes")

if animal and tk:
    outros = corpo(requests.get(f"{BASE}/api/veterinarios",
                                headers={"Authorization": f"Bearer {tk}"}, timeout=60)) or []
    externo = next((v["id"] for v in outros if v.get("id") != vet_id), None)
    if externo:
        r = chamar("POST", "/api/colmeia", token=tk, esperado=[201, 409], corpo={
            "animalId": animal, "veterinarioId": externo,
            "escopo": "HistoricoCompleto", "validadeEmDias": 30,
            "motivo": "Atendimento de emergencia em outra clinica."})
        acesso = (corpo(r) or {}).get("id")
        if acesso:
            chamar("DELETE", f"/api/colmeia/{acesso}", token=tk, esperado=[200, 204])

if vt and animal:
    r = chamar("POST", "/api/lembretes", token=vt, esperado=[200, 201], corpo={
        "animalId": animal, "tutorId": tid, "tipo": "Retorno",
        "dataEvento": (datetime.now(timezone.utc) + timedelta(days=5)).isoformat()})
    lem = (corpo(r) or {}).get("id")
    if lem:
        chamar("POST", f"/api/lembretes/{lem}/tentativa", token=vt, esperado=[200, 204])
        chamar("POST", f"/api/lembretes/{lem}/resposta", token=tk, esperado=[200, 204])

# -----------------------------------------------------------------------------
secao("Notificacoes: marcar como lida")

ns = corpo(requests.get(f"{BASE}/api/notificacoes/tutor/{tid}",
                        headers={"Authorization": f"Bearer {tk}"}, timeout=60))
lista = ns if isinstance(ns, list) else (ns or {}).get("itens", [])
if lista:
    chamar("POST", f"/api/notificacoes/{lista[0]['id']}/lida", token=tk, esperado=200)
    print(f"        notificacoes na caixa: {len(lista)}")
    for n in lista[:6]:
        print(f"          [{n.get('tipo')}] {n.get('titulo')}")

# -----------------------------------------------------------------------------
secao("Sessao: refresh rotativo e logout")

if estado.get("tutor_refresh"):
    r = chamar("POST", "/api/auth/refresh", esperado=200,
               corpo={"refreshToken": estado["tutor_refresh"]})
    novo = (corpo(r) or {}).get("refreshToken")
    chamar("POST", "/api/auth/refresh", esperado=[401, 422],
           corpo={"refreshToken": estado["tutor_refresh"]})
    if novo:
        chamar("POST", "/api/auth/logout", esperado=[200, 204], corpo={"refreshToken": novo})

# ─────────────────────────────────────────────────────────────────────────────
if not RAPIDO:
    secao("IA (Ollama) -- lento em CPU")
    chamar("POST", "/api/ia/diagnostico", token=vt, esperado=200, timeout=600, corpo={
        "especie": "Canino", "raca": "SRD", "idadeAnos": 4, "pesoKg": 28.5,
        "sintomas": ["vomito", "apatia"], "observacoes": "vacinacao em dia"})
    chamar("POST", "/api/ia/protocolo", token=vt, esperado=200, timeout=600, corpo={
        "diagnostico": "Gastroenterite", "especie": "Canino",
        "pesoKg": 28.5, "idadeAnos": 4})
    chamar("POST", "/api/ia/protocolo", token=vt, esperado=422, timeout=120, corpo={
        "diagnostico": "Gastroenterite", "especie": "Canino",
        "pesoKg": 0, "idadeAnos": 4})
    chamar("POST", "/api/ia/triagem", token=tk, esperado=200, timeout=600, corpo={
        "especie": "Canino", "sintomas": ["vomito"], "idadeAnos": 4})
    chamar("POST", "/api/ia/orientacoes", token=tk, esperado=200, timeout=600, corpo={
        "especie": "Canino", "raca": "Golden", "idadeAnos": 4, "pesoKg": 28.5})

# ─────────────────────────────────────────────────────────────────────────────
print("\n" + "=" * 78)
total = len(resultados)
falhas = [r for r in resultados if not r[4]]
print(f"RESUMO: {total - len(falhas)}/{total} verdes")

if falhas:
    print(f"\n{len(falhas)} FALHA(S):")
    for verbo, rota, esp, obt, _, det in falhas:
        print(f"  {verbo:6} {rota:58} esperado {esp}, obtido {obt}  {det}")
    sys.exit(1)

print("Tudo verde.")
