<#
.SYNOPSIS
    Provisiona uma VM no Azure e sobe a Vetly inteira nela: API, Ollama e proxy HTTPS.

.DESCRIPTION
    Tudo numa maquina so. A escolha nao e de arquitetura, e de restricao: o Ollama
    precisa de um processo residente com o modelo carregado na memoria, e nenhum
    servico gerenciado da Azure entrega isso barato. Numa VM, API e Ollama conversam
    pela rede interna do Docker, sem sair da maquina.

    O que fica publico e so a porta 443, no Caddy. A API (8080) e o Ollama (11434)
    vivem na rede interna do compose — o Ollama nao tem autenticacao nenhuma, e
    publica-lo seria entregar um LLM aberto a quem passar.

    O script e IDEMPOTENTE. Rodar de novo reaproveita a VM existente, recopia o codigo
    e refaz o build — que e exatamente o que se quer num redeploy.

    NAO aplica migrations: alterar schema e ato separado, feito contra o Oracle antes
    de a imagem nova subir.

.PARAMETER Sufixo
    Torna o FQDN unico. O endereco final e vetly-<sufixo>.<regiao>.cloudapp.azure.com.

.EXAMPLE
    ./deploy/azure-vm-deploy.ps1 -ConnectionStringOracle "User Id=...;Password=...;Data Source=oracle.fiap.com.br:1521/orcl"

.EXAMPLE
    ./deploy/azure-vm-deploy.ps1 -Sufixo vtly01 -ConnectionStringOracle "..." -SomenteAplicacao
    Recopia o codigo e refaz o build, sem tocar na infraestrutura.
#>

[CmdletBinding()]
param(
    [string]$Sufixo = ([guid]::NewGuid().ToString('N').Substring(0, 6)),
    [string]$GrupoDeRecursos = 'rg-vetly-vm',

    # canadacentral: uma das cinco regioes que a policy desta assinatura permite, e
    # onde ha quota. Dv5/DSv5 estao com quota ZERO aqui — dai a familia Dasv4.
    [string]$Regiao = 'canadacentral',

    # 4 vCPU e 16 GB. O teto de vCPU da assinatura e 6, e o llama3.1 (8B, quantizado)
    # ocupa 5-6 GB so de modelo: 8 GB nao sobrariam para a API e o sistema. Dedicada e
    # nao burstable porque inferencia e carga sustentada — numa B-series os creditos
    # acabam e a maquina cai para uma fracao do desempenho justo quando esta em uso.
    [string]$TamanhoDaVm = 'Standard_D4as_v4',

    [Parameter(Mandatory = $true)]
    [string]$ConnectionStringOracle,

    [string]$JwtKey,
    [string]$TokenInterno,

    # Modelo que o Ollama baixa. llama3.1 e o do appsettings do projeto.
    [string]$ModeloDeIa = 'llama3.1',

    # Chave do Azure Speech. Sem ela, o adaptador de transcricao fica em Simulado.
    [string]$AzureSpeechKey,
    [string]$AzureSpeechRegion = 'canadacentral',

    [switch]$SomenteAplicacao
)

$ErrorActionPreference = 'Stop'

# ── Localiza o az ─────────────────────────────────────────────────────────────
$Az = (Get-Command az -ErrorAction SilentlyContinue).Source
if (-not $Az) {
    $Az = @(
        "$env:ProgramFiles\Microsoft SDKs\Azure\CLI2\wbin\az.cmd",
        "${env:ProgramFiles(x86)}\Microsoft SDKs\Azure\CLI2\wbin\az.cmd"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Az) { throw 'Azure CLI nao encontrado. winget install Microsoft.AzureCLI' }

$vm         = "vetly-vm-$Sufixo"
$rotuloDns  = "vetly-$Sufixo"
$fqdn       = "$rotuloDns.$Regiao.cloudapp.azure.com"
$usuario    = 'azureuser'
$raiz       = Split-Path $PSScriptRoot -Parent

function Passo($t) { Write-Host "`n=== $t" -ForegroundColor Cyan }
function Ok($t)    { Write-Host "    $t" -ForegroundColor DarkGray }

function Invoke-Az {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Argumentos)
    $saida = & $Az @Argumentos
    if ($LASTEXITCODE -ne 0) { throw "az $($Argumentos -join ' ') falhou com codigo $LASTEXITCODE" }
    return $saida
}

# Conferencia de existencia por `list`, e nao `show`: no PowerShell 5.1 redirecionar o
# stderr de um executavel nativo vira ErrorRecord e, com ErrorActionPreference Stop,
# derruba o script no caminho em que o recurso ainda nao existe.
function Test-Recurso {
    param([string]$Nome, [string]$Tipo)
    $achados = Invoke-Az resource list --resource-group $GrupoDeRecursos `
        --resource-type $Tipo --output json | ConvertFrom-Json
    return ($null -ne ($achados | Where-Object { $_.name -eq $Nome }))
}

function New-Segredo {
    param([int]$Bytes = 48)
    $b = New-Object byte[] $Bytes
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
    return [Convert]::ToBase64String($b)
}

<#
.SYNOPSIS
    Roda um comando na VM por SSH, falhando o script quando o comando falha.
#>
function Invoke-Ssh {
    param([string]$Comando, [switch]$IgnorarFalha)

    $saida = ssh -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null `
        -o LogLevel=ERROR "$usuario@$fqdn" $Comando 2>&1

    $saida | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }

    if ($LASTEXITCODE -ne 0 -and -not $IgnorarFalha) {
        throw "Comando remoto falhou (codigo $LASTEXITCODE): $Comando"
    }
    return $saida
}

# ── 0. Sessao ─────────────────────────────────────────────────────────────────
Passo '0. Sessao do Azure CLI'

$conta = & $Az account show --output json 2>$null | ConvertFrom-Json
if ($null -eq $conta) { throw "Sem sessao ativa. Rode 'az login'." }
Ok "Assinatura: $($conta.name)"

if (-not $JwtKey)       { $JwtKey       = New-Segredo -Bytes 48 }
if (-not $TokenInterno) { $TokenInterno = New-Segredo -Bytes 32 }

if (-not $SomenteAplicacao) {

    # ── 1. Grupo ──────────────────────────────────────────────────────────────
    Passo "1. Grupo de recursos ($GrupoDeRecursos em $Regiao)"

    if ((Invoke-Az group exists --name $GrupoDeRecursos) -eq 'true') {
        Ok 'Ja existe.'
    } else {
        Invoke-Az group create --name $GrupoDeRecursos --location $Regiao --output none | Out-Null
        Ok 'Criado.'
    }

    # ── 2. VM ─────────────────────────────────────────────────────────────────
    Passo "2. Maquina virtual ($vm, $TamanhoDaVm)"

    if (Test-Recurso -Nome $vm -Tipo 'Microsoft.Compute/virtualMachines') {
        Ok 'Ja existe — nao recriada.'
    } else {
        $cloudInit = Join-Path $PSScriptRoot 'vm/cloud-init.yaml'
        if (-not (Test-Path $cloudInit)) { throw "cloud-init nao encontrado em $cloudInit" }

        # StandardSSD no disco: o gargalo desta maquina e CPU, nao IOPS. Premium SSD
        # custaria o dobro para acelerar algo que nao esta lento.
        Invoke-Az vm create `
            --resource-group $GrupoDeRecursos --name $vm --location $Regiao `
            --image Ubuntu2204 --size $TamanhoDaVm `
            --admin-username $usuario --generate-ssh-keys `
            --public-ip-address-dns-name $rotuloDns `
            --public-ip-sku Standard `
            --os-disk-size-gb 64 --storage-sku StandardSSD_LRS `
            --custom-data $cloudInit `
            --nsg-rule SSH --output none | Out-Null
        Ok "Criada. FQDN: $fqdn"
    }

    # ── 3. Portas ─────────────────────────────────────────────────────────────
    # So 80 e 443. A 80 e necessaria: o Let's Encrypt valida o dominio por HTTP antes
    # de emitir o certificado. A 11434 do Ollama NAO entra aqui.
    Passo '3. Regras de rede (80 e 443)'

    Invoke-Az vm open-port --resource-group $GrupoDeRecursos --name $vm `
        --port 80 --priority 1010 --output none | Out-Null
    Invoke-Az vm open-port --resource-group $GrupoDeRecursos --name $vm `
        --port 443 --priority 1020 --output none | Out-Null
    Ok 'Abertas 80 (validacao ACME) e 443 (API). Ollama segue interno.'

    # ── 4. Espera o cloud-init ────────────────────────────────────────────────
    Passo '4. Aguardando a preparacao da VM (Docker)'
    Ok 'O cloud-init instala o Docker no primeiro boot. Pode levar 2-4 minutos.'

    $limite = (Get-Date).AddMinutes(10)
    $pronto = $false

    while ((Get-Date) -lt $limite) {
        $r = ssh -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null `
            -o LogLevel=ERROR -o ConnectTimeout=10 "$usuario@$fqdn" `
            'test -f /opt/vetly/pronto && echo PRONTO' 2>$null

        if ($r -match 'PRONTO') { $pronto = $true; break }
        Start-Sleep -Seconds 15
    }

    if (-not $pronto) {
        throw "A VM nao ficou pronta em 10 minutos. Investigue com: ssh $usuario@$fqdn 'sudo cat /var/log/cloud-init-output.log'"
    }
    Ok 'Docker instalado e pronto.'
}

# ── 5. Envio do codigo ────────────────────────────────────────────────────────
Passo '5. Enviando o codigo para a VM'

# git archive em vez de tar manual: leva exatamente os arquivos versionados, entao
# bin/, obj/, logs/ e os appsettings.*.local.json ficam de fora sem lista de exclusao
# para manter em dia.
$pacote = Join-Path ([IO.Path]::GetTempPath()) "vetly-$Sufixo.tar"

Push-Location $raiz
try {
    git archive --format=tar --output=$pacote HEAD
    if ($LASTEXITCODE -ne 0) { throw 'git archive falhou. Ha commits pendentes?' }
} finally {
    Pop-Location
}

Ok ("Pacote: {0:N1} MB" -f ((Get-Item $pacote).Length / 1MB))

scp -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o LogLevel=ERROR `
    $pacote "${usuario}@${fqdn}:/tmp/vetly.tar"
if ($LASTEXITCODE -ne 0) { throw 'scp do pacote falhou.' }

Remove-Item $pacote -Force

Invoke-Ssh 'rm -rf /opt/vetly/app && mkdir -p /opt/vetly/app && tar -xf /tmp/vetly.tar -C /opt/vetly/app && rm /tmp/vetly.tar'
Ok 'Codigo extraido em /opt/vetly/app.'

# ── 6. Configuracao ───────────────────────────────────────────────────────────
Passo '6. Configuracao da aplicacao'

$url = "https://$fqdn"
$stt = if ($AzureSpeechKey) { 'Azure' } else { 'Simulado' }

# O .env nasce com 600 e fica so na VM. Nada disso passa pelo repositorio.
$env_ = @"
VETLY_FQDN=$fqdn
ConnectionStrings__OracleConnection=$ConnectionStringOracle
Jwt__Key=$JwtKey
Jwt__Issuer=Vetly
Jwt__Audience=VetlyAPI
Servicos__TokenInterno=$TokenInterno
Servicos__CallbackBaseUrl=$url
Storage__PublicBaseUrl=$url
Ollama__Model=$ModeloDeIa
Adaptadores__Stt=$stt
"@

if ($AzureSpeechKey) {
    $env_ += "AZURE_SPEECH_KEY=$AzureSpeechKey`nAZURE_SPEECH_REGION=$AzureSpeechRegion`n"
}

# base64 no transporte: o conteudo tem '=' e '/' das chaves e ';' da connection
# string, que o shell remoto interpretaria.
$b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($env_))

Invoke-Ssh "echo '$b64' | base64 -d > /opt/vetly/app/deploy/vm/.env && chmod 600 /opt/vetly/app/deploy/vm/.env"
Ok "STT: $stt | Modelo de IA: $ModeloDeIa"

# ── 7. Build e subida ─────────────────────────────────────────────────────────
Passo '7. Build da imagem e subida dos containers'
Ok 'O primeiro build restaura o NuGet e compila — 3 a 6 minutos.'

Invoke-Ssh 'cd /opt/vetly/app/deploy/vm && docker compose up -d --build'
Ok 'Containers no ar.'

# ── 8. Modelo de IA ───────────────────────────────────────────────────────────
Passo "8. Baixando o modelo $ModeloDeIa no Ollama"
Ok 'Sao ~5 GB. Pode levar alguns minutos.'

Invoke-Ssh "cd /opt/vetly/app/deploy/vm && docker compose exec -T ollama ollama pull $ModeloDeIa"
Ok 'Modelo disponivel.'

# ── 9. Verificacao ────────────────────────────────────────────────────────────
Passo '9. Conferindo a saude'
Ok 'O Caddy precisa emitir o certificado no primeiro acesso. Aguardando...'

$saude = $null
$limite = (Get-Date).AddMinutes(5)

while ((Get-Date) -lt $limite) {
    try {
        $saude = Invoke-RestMethod -Uri "$url/health/ready" -TimeoutSec 20
        break
    } catch {
        # 503 e resposta legitima do readiness: o corpo diz qual dependencia caiu.
        $resp = $_.Exception.Response
        if ($resp) {
            $leitor = New-Object IO.StreamReader($resp.GetResponseStream())
            $corpo = $leitor.ReadToEnd()
            if ($corpo) { $saude = $corpo | ConvertFrom-Json; break }
        }
        Start-Sleep -Seconds 15
    }
}

if ($saude) {
    Write-Host "`n    Estado geral: $($saude.status)" -ForegroundColor Yellow
    foreach ($c in $saude.checks) {
        $cor = if ($c.status -eq 'Healthy') { 'Green' } else { 'Red' }
        Write-Host ("    {0,-16} {1}" -f $c.name, $c.status) -ForegroundColor $cor
    }
} else {
    Write-Host '    Nao respondeu a tempo. Veja: ssh ' -NoNewline -ForegroundColor Red
    Write-Host "$usuario@$fqdn 'cd /opt/vetly/app/deploy/vm && docker compose logs --tail=80'" -ForegroundColor Red
}

Passo 'Pronto'
Write-Host @"

    API       $url
    Scalar    $url/scalar/v1
    Saude     $url/health/ready

    SSH       ssh $usuario@$fqdn
    Logs      ssh $usuario@$fqdn 'cd /opt/vetly/app/deploy/vm && docker compose logs -f'

    Sufixo deste ambiente: $Sufixo

    Os segredos gerados vivem so no .env dentro da VM:
      ssh $usuario@$fqdn 'sudo cat /opt/vetly/app/deploy/vm/.env'

    PARA PARAR DE PAGAR quando nao estiver usando (a maior alavanca de custo):
      az vm deallocate -g $GrupoDeRecursos -n $vm
      az vm start      -g $GrupoDeRecursos -n $vm

    Desalocada, a VM para de ser cobrada; ficam so o disco e o IP (~US\$ 8/mes).

"@ -ForegroundColor Green
