using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.API.Jobs;

/// <summary>
/// Cria o primeiro administrador da plataforma, uma única vez, a partir da
/// configuração (§4.1).
///
/// <para>
/// <b>O problema que isto resolve.</b> Administrador é o veterinário apontado em
/// <see cref="Empresa.AdministradorId"/>, e criar empresa exige a role
/// <c>Admin</c> — que só existe para quem já administra uma empresa. Sem um ponto de
/// partida, a plataforma sobe em produção com a persona de administração inalcançável:
/// não há como cadastrar veterinário, criar unidade, ver o consolidado financeiro nem
/// redistribuir consulta. O ovo nunca chega à galinha.
/// </para>
/// <para>
/// <b>Por que na configuração e não numa rota.</b> Uma rota de bootstrap seria uma
/// porta pública que precisa se recusar a funcionar depois da primeira vez, e essa é
/// a categoria de código que envelhece mal. A configuração já é o canal por onde o
/// operador entrega segredo a esta aplicação, e quem controla o ambiente é quem tem o
/// direito de nomear o primeiro administrador.
/// </para>
/// <para>
/// <b>Idempotente e conservador.</b> Só age quando <c>Bootstrap:AdminEmail</c> está
/// definido E não existe nenhuma empresa. Havendo qualquer unidade cadastrada, a
/// plataforma já tem administração e o semeador se cala — sem isso, um reinício
/// duplicaria unidade e um redeploy poderia ressuscitar um acesso que alguém removeu
/// de propósito.
/// </para>
/// </summary>
public class SemeadorDoAdministrador : IHostedService
{
    private readonly IServiceProvider _servicos;
    private readonly IConfiguration _config;
    private readonly ILogger<SemeadorDoAdministrador> _logger;

    public SemeadorDoAdministrador(
        IServiceProvider servicos, IConfiguration config, ILogger<SemeadorDoAdministrador> logger)
    {
        _servicos = servicos;
        _config = config;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var email = _config["Bootstrap:AdminEmail"];

        if (string.IsNullOrWhiteSpace(email))
            return;

        var senha = _config["Bootstrap:AdminSenha"];

        if (string.IsNullOrWhiteSpace(senha))
        {
            // Ruidoso de proposito: quem definiu o e-mail queria o semeador ligado, e
            // um bootstrap que desiste em silencio deixa a plataforma sem
            // administracao com a aparencia de que tudo correu bem.
            _logger.LogError(
                "Bootstrap:AdminEmail definido sem Bootstrap:AdminSenha. " +
                "O administrador inicial NAO foi criado.");
            return;
        }

        // Escopo proprio: este servico e singleton e os repositorios sao scoped.
        using var escopo = _servicos.CreateScope();
        var provedor = escopo.ServiceProvider;

        var empresaRepo = provedor.GetRequiredService<IEmpresaRepository>();
        var vetRepo = provedor.GetRequiredService<IVeterinarioRepository>();
        var hasher = provedor.GetRequiredService<ISenhaHasher>();

        try
        {
            if ((await empresaRepo.ObterAtivasAsync()).Any())
            {
                _logger.LogInformation(
                    "Ja existe unidade cadastrada; o administrador inicial nao foi recriado.");
                return;
            }

            var nome = _config["Bootstrap:AdminNome"] ?? "Administrador Vetly";
            var crmv = _config["Bootstrap:AdminCrmv"] ?? "00001-SP";
            var uf = _config["Bootstrap:AdminUf"] ?? "SP";
            var nomeDaEmpresa = _config["Bootstrap:EmpresaNome"] ?? "Unidade Vetly";
            var tipoDaEmpresa = _config["Bootstrap:EmpresaTipo"] ?? "Clinica";

            var plano = Enum.TryParse<PlanoAssinatura>(_config["Bootstrap:EmpresaPlano"], out var p)
                ? p
                : PlanoAssinatura.Enterprise;

            // E-mail ja cadastrado: reaproveita o veterinario em vez de falhar. O caso
            // real e o operador rodar o bootstrap depois de ja ter criado o vet por
            // outro caminho — recusar ali nao protegeria nada e travaria o deploy.
            var vet = await vetRepo.ObterPorEmailAsync(email);

            if (vet is null)
            {
                vet = new Veterinario(nome, new Crmv(crmv), uf, PersonaVeterinario.Vinculado, plano);
                vet.DefinirEmail(email);

                // Temporaria: o login avisa `senhaTemporaria: true` e o app conduz a
                // troca. A senha do bootstrap passou por variavel de ambiente e por
                // log de deploy — nao e segredo que deva durar.
                vet.DefinirSenhaHash(hasher.GerarHash(senha), temporaria: true);

                await vetRepo.AdicionarAsync(vet);
                await vetRepo.SalvarAsync();
            }

            var empresa = new Empresa(nomeDaEmpresa, tipoDaEmpresa, vet.Id, plano);

            await empresaRepo.AdicionarAsync(empresa);
            await empresaRepo.SalvarAsync();

            // A role Admin nao e gravada: ela e derivada deste vinculo no login.
            _logger.LogWarning(
                "Administrador inicial criado: {Email} administra a unidade {Unidade}. " +
                "A senha e TEMPORARIA — troque-a por POST /api/auth/trocar-senha e remova " +
                "Bootstrap:AdminSenha da configuracao.",
                email, nomeDaEmpresa);
        }
        catch (Exception excecao)
        {
            // O bootstrap nao pode derrubar a API. Sem banco no arranque, o
            // /health/ready ja denuncia — e a plataforma subir sem administrador e um
            // problema menor do que nao subir.
            _logger.LogError(excecao, "Falha ao criar o administrador inicial.");
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
