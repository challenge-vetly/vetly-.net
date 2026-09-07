# syntax=docker/dockerfile:1

# ─────────────────────────────────────────────────────────────────────────────
# Vetly API — imagem de execucao
#
# Multi-stage: o SDK compila, a imagem final carrega so o runtime do ASP.NET. A
# diferenca nao e estetica — o SDK traz compilador, NuGet e as fontes, e nada disso
# tem por que existir num container que so atende HTTP.
#
#   docker build -t vetly-api .
#   docker run -p 8080:8080 \
#     -e ConnectionStrings__OracleConnection="User Id=...;Password=...;Data Source=host:1521/orcl" \
#     -e Jwt__Key="uma-chave-com-no-minimo-32-caracteres" \
#     -e Servicos__TokenInterno="um-token-de-servico" \
#     -e Storage__PublicBaseUrl="https://api.seudominio.com" \
#     vetly-api
#
# As migrations NAO sao aplicadas na subida: nao ha Database.Migrate() no codigo, e o
# passo continua manual e deliberado (ver o README). Nenhuma instancia altera o schema
# por conta propria ao subir — com varias replicas, seria cada uma tentando ao mesmo
# tempo.
# ─────────────────────────────────────────────────────────────────────────────

# ── Estagio 1: restore e build ───────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Os .csproj entram sozinhos primeiro, e depois o resto do codigo. E o que faz a
# camada do "dotnet restore" sobreviver no cache a qualquer mudanca de codigo que nao
# mexa em dependencia — que e a mudanca de 99% dos builds.
#
# O arquivo de solucao e Vetly.slnx (formato XML do .NET 9+), e nao Vetly.sln. Um
# "COPY *.sln" aqui casa com nada e o build morre adiante, sem dizer por que.
COPY Vetly.slnx ./
COPY src/Vetly.Domain/Vetly.Domain.csproj                   src/Vetly.Domain/
COPY src/Vetly.Application/Vetly.Application.csproj         src/Vetly.Application/
COPY src/Vetly.Infrastructure/Vetly.Infrastructure.csproj   src/Vetly.Infrastructure/
COPY src/Vetly.API/Vetly.API.csproj                         src/Vetly.API/

# So o projeto da API: os testes nao entram na imagem, e restaurar as dependencias
# deles traria xUnit e Moq para dentro do build de producao.
RUN dotnet restore src/Vetly.API/Vetly.API.csproj

COPY src/ src/

RUN dotnet publish src/Vetly.API/Vetly.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:UseAppHost=false

# ── Estagio 2: execucao ──────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# 8080 e a porta padrao das imagens do .NET 8+, escolhida justamente por estar acima
# de 1024: porta baixa exigiria root, e a imagem roda como usuario sem privilegio.
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=1

# O storage local grava audio de consulta e PDF de documento. Sem um caminho explicito
# o adaptador cai no diretorio temporario, que some junto com o container — e o audio
# de uma consulta em andamento sumiria no meio dela. Monte um volume aqui em producao,
# ou troque Adaptadores:Storage por um bucket quando ele existir.
ENV Storage__Diretorio=/var/lib/vetly/storage
VOLUME /var/lib/vetly/storage

COPY --from=build /app/publish .

# Usuario sem privilegio, dono so do que precisa escrever. A imagem base ja traz o
# usuario "app" (uid 1654); o que falta e ele poder gravar no storage e nos logs.
RUN mkdir -p /var/lib/vetly/storage /app/logs \
    && chown -R app:app /var/lib/vetly /app/logs
USER app

# Sem HEALTHCHECK aqui, e de proposito. A imagem base do ASP.NET nao traz curl nem
# wget, entao um HEALTHCHECK sondaria de dentro do container sem ter com o que sondar —
# e instalar um cliente HTTP so para isso engorda a imagem para resolver algo que o
# orquestrador ja faz melhor de fora. As sondas existem e sao HTTP:
#
#   /health/live   → o processo esta vivo? (liveness — decide REINICIAR)
#   /health/ready  → as dependencias respondem? (readiness — decide receber TRAFEGO)
#
# Oracle fora do ar deixa o /health/ready em 503 e o /health/live em 200, que e o
# comportamento certo: reiniciar a API nao levanta o banco.
ENTRYPOINT ["dotnet", "Vetly.API.dll"]
