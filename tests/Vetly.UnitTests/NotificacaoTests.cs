using Moq;
using Vetly.Application.DTOs.Notificacao;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Notificacoes ao Responsavel e a entrega por push (RN-092/RN-093).
/// </summary>
public class NotificacaoTests
{
    private readonly Mock<INotificacaoRepository> _repo = new();
    private readonly Mock<IDispositivoRepository> _dispositivos = new();
    private readonly Mock<IPushAdapter> _push = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Guid _tutorId = Guid.NewGuid();

    public NotificacaoTests()
    {
        _usuario.SetupGet(u => u.EhTutor).Returns(true);
        _usuario.SetupGet(u => u.TutorId).Returns(_tutorId);

        _repo.Setup(r => r.AdicionarAsync(It.IsAny<Notificacao>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _dispositivos.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _dispositivos.Setup(r => r.ObterAtivosDoTutorAsync(It.IsAny<Guid>())).ReturnsAsync([]);

        _push.Setup(p => p.EnviarAsync(It.IsAny<EnvioDePushRequest>()))
            .ReturnsAsync(new ResultadoDoPushDto(Entregue: true, Erro: null, TokenInvalido: false));
    }

    private NotificacaoService CriarServico() =>
        new(_repo.Object, _dispositivos.Object, _push.Object, _usuario.Object);

    private Notificacao Notificacao(DateTime? agendadaPara = null)
    {
        var notificacao = new Notificacao(
            _tutorId, TipoNotificacao.ObrigacaoVencendo, "Cuidados chegando",
            "V10 vence em breve.", agendadaPara);

        _repo.Setup(r => r.ObterPorIdAsync(notificacao.Id)).ReturnsAsync(notificacao);

        return notificacao;
    }

    private Dispositivo ComDispositivo(string token = "token-de-push-valido")
    {
        var dispositivo = new Dispositivo(_tutorId, token, PlataformaDispositivo.Android);

        _dispositivos.Setup(r => r.ObterAtivosDoTutorAsync(_tutorId)).ReturnsAsync([dispositivo]);

        return dispositivo;
    }

    // ── Criação e caixa de entrada (RN-092) ──────────────────────────────────

    [Fact]
    public async Task Notificacao_NasceGravadaEPendente()
    {
        var dto = await CriarServico().CriarAsync(new CriarNotificacaoDto
        {
            TutorId = _tutorId,
            Tipo = TipoNotificacao.DocumentoPublicado,
            Titulo = "Documento pronto",
            Corpo = "A receita do Thor esta disponivel."
        });

        // Gravada antes de enviada: o app precisa de uma caixa que sobrevive ao push
        // perdido
        Assert.Equal(StatusNotificacao.Pendente, dto.Status);
        _repo.Verify(r => r.AdicionarAsync(It.IsAny<Notificacao>()), Times.Once);
    }

    [Fact]
    public async Task Notificacao_SemTitulo_NaoEAceita()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CriarServico().CriarAsync(new CriarNotificacaoDto
        {
            TutorId = _tutorId, Tipo = TipoNotificacao.ConsultaProxima, Titulo = "  ", Corpo = "corpo"
        }));
    }

    [Fact]
    public async Task CaixaDeEntrada_DeOutroResponsavel_ERecusada()
    {
        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ObterCaixaDeEntradaAsync(Guid.NewGuid(), false));

        Assert.Equal("RN-106", ex.Codigo);
    }

    [Fact]
    public async Task Leitura_GuardaSomenteAPrimeira()
    {
        var notificacao = Notificacao();

        var servico = CriarServico();
        var primeira = await servico.MarcarComoLidaAsync(notificacao.Id);
        var segunda = await servico.MarcarComoLidaAsync(notificacao.Id);

        // E o dado que diz se o aviso chegou a quem cuida do animal
        Assert.Equal(primeira.LidaEm, segunda.LidaEm);
        Assert.Equal(StatusNotificacao.Lida, segunda.Status);
    }

    // ── Entrega por push (RN-092) ────────────────────────────────────────────

    [Fact]
    public async Task Entrega_ComDispositivoAtivo_MarcaComoEnviada()
    {
        var notificacao = Notificacao();
        ComDispositivo();

        var entregue = await CriarServico().EntregarAsync(notificacao.Id);

        Assert.True(entregue);
        Assert.Equal(StatusNotificacao.Enviada, notificacao.Status);
        Assert.NotNull(notificacao.EnviadaEm);
    }

    [Fact]
    public async Task Entrega_SemDispositivo_NaoDescartaANotificacao()
    {
        var notificacao = Notificacao();

        var entregue = await CriarServico().EntregarAsync(notificacao.Id);

        // Push perdido nao pode significar aviso perdido: a notificacao continua na
        // caixa de entrada do app
        Assert.False(entregue);
        Assert.Equal(StatusNotificacao.Pendente, notificacao.Status);
        Assert.Equal(1, notificacao.Tentativas);
    }

    [Fact]
    public async Task Entrega_TokenInvalido_DesativaODispositivo()
    {
        var notificacao = Notificacao();
        var dispositivo = ComDispositivo("x");

        _push.Setup(p => p.EnviarAsync(It.IsAny<EnvioDePushRequest>()))
            .ReturnsAsync(new ResultadoDoPushDto(false, "Token invalido", TokenInvalido: true));

        await CriarServico().EntregarAsync(notificacao.Id);

        // App desinstalado e token rotacionado sao o caso comum, nao a excecao:
        // retentar para sempre um endereco morto nao ajuda ninguem
        Assert.False(dispositivo.Ativo);
    }

    [Fact]
    public async Task Entrega_FalhaDoProvedor_NaoDesativaODispositivo()
    {
        var notificacao = Notificacao();
        var dispositivo = ComDispositivo();

        _push.Setup(p => p.EnviarAsync(It.IsAny<EnvioDePushRequest>()))
            .ReturnsAsync(new ResultadoDoPushDto(false, "Provedor indisponivel", TokenInvalido: false));

        await CriarServico().EntregarAsync(notificacao.Id);

        // Provedor fora do ar e problema do provedor, nao do aparelho
        Assert.True(dispositivo.Ativo);
        Assert.Equal(StatusNotificacao.Pendente, notificacao.Status);
    }

    [Fact]
    public async Task Entrega_DepoisDeTresFalhas_DesisteDoCanalMasMantemNaCaixa()
    {
        var notificacao = Notificacao();

        var servico = CriarServico();

        for (var i = 0; i < Domain.Entities.Notificacao.MaximoDeTentativas; i++)
            await servico.EntregarAsync(notificacao.Id);

        Assert.Equal(StatusNotificacao.NaoEntregue, notificacao.Status);

        // NaoEntregue nao e o fim: a linha segue visivel na caixa de entrada
        Assert.False(notificacao.Alcancou());
    }

    [Fact]
    public async Task Entrega_AgendadaParaOFuturo_AindaNaoSai()
    {
        var notificacao = Notificacao(agendadaPara: DateTime.UtcNow.AddHours(3));
        ComDispositivo();

        var entregue = await CriarServico().EntregarAsync(notificacao.Id);

        Assert.False(entregue);
        Assert.Equal(0, notificacao.Tentativas);
        _push.Verify(p => p.EnviarAsync(It.IsAny<EnvioDePushRequest>()), Times.Never);
    }

    [Fact]
    public async Task Entrega_JaEnviada_NaoEnviaDeNovo()
    {
        var notificacao = Notificacao();
        notificacao.RegistrarEnvio(DateTime.UtcNow);
        ComDispositivo();

        Assert.False(await CriarServico().EntregarAsync(notificacao.Id));
        _push.Verify(p => p.EnviarAsync(It.IsAny<EnvioDePushRequest>()), Times.Never);
    }

    [Fact]
    public async Task Entrega_LevaODestinoParaOApp()
    {
        var notificacao = new Notificacao(
            _tutorId, TipoNotificacao.ObrigacaoVencendo, "Cuidados", "corpo",
            destino: "/animais/abc/obrigacoes");

        _repo.Setup(r => r.ObterPorIdAsync(notificacao.Id)).ReturnsAsync(notificacao);
        ComDispositivo();

        EnvioDePushRequest? enviado = null;
        _push.Setup(p => p.EnviarAsync(It.IsAny<EnvioDePushRequest>()))
            .Callback<EnvioDePushRequest>(r => enviado = r)
            .ReturnsAsync(new ResultadoDoPushDto(true, null, false));

        await CriarServico().EntregarAsync(notificacao.Id);

        // O destino e rota interna, e nao URL: para onde ir e do app, nao da API
        Assert.Equal("/animais/abc/obrigacoes", enviado!.Value.Destino);
    }
}
