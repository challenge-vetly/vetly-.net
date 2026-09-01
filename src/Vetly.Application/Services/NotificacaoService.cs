using Vetly.Application.DTOs.Notificacao;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Notificações ao Responsável e a entrega por push (RN-092/RN-093).
///
/// A notificação é gravada antes de ser enviada. O app precisa de uma caixa de
/// entrada que sobrevive ao push perdido — dispositivo desligado, token trocado,
/// permissão negada — e o histórico do que foi comunicado é o que permite responder
/// "avisamos?" depois.
/// </summary>
public class NotificacaoService : INotificacaoService
{
    private readonly INotificacaoRepository _repo;
    private readonly IDispositivoRepository _dispositivoRepo;
    private readonly IPushAdapter _push;
    private readonly IUsuarioAtual _usuario;
    private readonly ITutorRepository _tutorRepo;

    public NotificacaoService(
        INotificacaoRepository repo,
        IDispositivoRepository dispositivoRepo,
        IPushAdapter push,
        IUsuarioAtual usuario,
        ITutorRepository tutorRepo)
    {
        _repo = repo;
        _dispositivoRepo = dispositivoRepo;
        _push = push;
        _usuario = usuario;
        _tutorRepo = tutorRepo;
    }

    /// <inheritdoc/>
    public async Task<PreferenciasDeNotificacaoDto> ObterPreferenciasAsync()
    {
        var tutor = await TutorDoTokenAsync();

        return Mapear(tutor);
    }

    /// <inheritdoc/>
    public async Task<PreferenciasDeNotificacaoDto> AtualizarPreferenciasAsync(AtualizarPreferenciasDto dto)
    {
        var tutor = await TutorDoTokenAsync();

        // Escrever no registro de consentimento, e nao numa coluna de preferencia,
        // mantem uma unica fonte da vontade do Responsavel — e e essa que vale
        // juridicamente (RN-061/RN-093).
        tutor.RegistrarConsentimento(
            FinalidadeConsentimento.Promocoes, dto.AceitaPromocoes, DateTime.UtcNow);

        _tutorRepo.Atualizar(tutor);
        await _tutorRepo.SalvarAsync();

        return Mapear(tutor);
    }

    /// <summary>
    /// As preferências são do próprio Responsável. O id vem do token: aceitar um
    /// parâmetro aqui deixaria qualquer um desligar as promoções de outro — ou, pior,
    /// religá-las (RN-106).
    /// </summary>
    private async Task<Tutor> TutorDoTokenAsync()
    {
        if (_usuario.TutorId is not { } tutorId)
            throw new AcessoNegadoException("RN-106",
                "As preferencias de notificacao sao do Responsavel.");

        return await _tutorRepo.ObterPorIdAsync(tutorId)
            ?? throw new NotFoundException("Tutor", tutorId);
    }

    private static PreferenciasDeNotificacaoDto Mapear(Tutor tutor) => new()
    {
        TutorId = tutor.Id,
        AceitaPromocoes = tutor.Consentiu(FinalidadeConsentimento.Promocoes),

        // A mais recente das duas datas, e nao a maior por comparacao direta: em C#
        // toda comparacao com null e falsa, entao "concessao > revogacao" devolveria
        // a revogacao — nula — justo no caso em que a preferencia acabou de ser ligada
        // pela primeira vez.
        AtualizadoEm = MaisRecente(tutor.DataConcessaoPromocoes, tutor.DataRevogacaoPromocoes)
    };

    /// <summary>A mais recente de duas datas opcionais. Nula quando nenhuma existe.</summary>
    private static DateTime? MaisRecente(DateTime? a, DateTime? b) =>
        (a, b) switch
        {
            (null, null) => null,
            (null, _) => b,
            (_, null) => a,
            _ => a > b ? a : b
        };

    /// <inheritdoc/>
    public async Task<NotificacaoDto> CriarAsync(CriarNotificacaoDto dto)
    {
        // RN-093: promocao e o unico tipo que o Responsavel desliga, e o padrao e
        // desligado. Aviso de consulta, documento publicado e obrigacao vencendo nao
        // sao opcionais — sao o servico que ele contratou, e desliga-los faria o app
        // deixar de avisar sobre a saude do animal.
        //
        // O opt-in vive no consentimento de LGPD, e nao numa coluna propria: duas
        // fontes para a mesma vontade acabariam discordando, e a que vale juridicamente
        // e o registro de consentimento.
        if (dto.Tipo == TipoNotificacao.Promocao)
        {
            var tutor = await _tutorRepo.ObterPorIdAsync(dto.TutorId)
                ?? throw new NotFoundException("Tutor", dto.TutorId);

            if (!tutor.Consentiu(FinalidadeConsentimento.Promocoes))
                throw new BusinessRuleException("RN-093",
                    "O Responsavel nao autorizou comunicacoes promocionais.");
        }

        var notificacao = new Notificacao(
            dto.TutorId, dto.Tipo, dto.Titulo, dto.Corpo,
            dto.AgendadaPara, dto.AnimalId, dto.ConsultaId, dto.Destino);

        await _repo.AdicionarAsync(notificacao);
        await _repo.SalvarAsync();

        return Mapear(notificacao);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<NotificacaoDto>> ObterCaixaDeEntradaAsync(Guid tutorId, bool apenasNaoLidas)
    {
        GarantirEscopo(tutorId);

        var notificacoes = await _repo.ObterDoTutorAsync(tutorId, apenasNaoLidas);

        return notificacoes.Select(Mapear);
    }

    /// <inheritdoc/>
    public async Task<NotificacaoDto> MarcarComoLidaAsync(Guid notificacaoId)
    {
        var notificacao = await _repo.ObterPorIdAsync(notificacaoId)
            ?? throw new NotFoundException("Notificacao", notificacaoId);

        GarantirEscopo(notificacao.TutorId);

        notificacao.MarcarComoLida(DateTime.UtcNow);

        _repo.Atualizar(notificacao);
        await _repo.SalvarAsync();

        return Mapear(notificacao);
    }

    /// <inheritdoc/>
    public async Task<bool> EntregarAsync(Guid notificacaoId)
    {
        var notificacao = await _repo.ObterPorIdAsync(notificacaoId)
            ?? throw new NotFoundException("Notificacao", notificacaoId);

        if (!notificacao.PodeEnviar(DateTime.UtcNow))
            return false;

        var dispositivos = (await _dispositivoRepo.ObterAtivosDoTutorAsync(notificacao.TutorId)).ToList();

        if (dispositivos.Count == 0)
        {
            // Sem dispositivo não há push, mas a notificação continua na caixa de
            // entrada: o Responsável a vê quando abrir o app (RN-092).
            notificacao.RegistrarFalha("Nenhum dispositivo ativo para push.");
            _repo.Atualizar(notificacao);
            await _repo.SalvarAsync();

            return false;
        }

        var entregue = false;

        foreach (var dispositivo in dispositivos)
        {
            var resultado = await _push.EnviarAsync(new EnvioDePushRequest(
                dispositivo.PushToken, notificacao.Titulo, notificacao.Corpo,
                notificacao.Destino, notificacao.Id));

            if (resultado.Entregue)
            {
                entregue = true;
                continue;
            }

            // Token morto é desativado em vez de retentado para sempre: app
            // desinstalado e token rotacionado são o caso comum, não a exceção.
            if (resultado.TokenInvalido)
            {
                dispositivo.Desativar();
                _dispositivoRepo.Atualizar(dispositivo);
            }
        }

        if (entregue)
            notificacao.RegistrarEnvio(DateTime.UtcNow);
        else
            notificacao.RegistrarFalha("Nenhum dispositivo aceitou o push.");

        _repo.Atualizar(notificacao);
        await _repo.SalvarAsync();
        await _dispositivoRepo.SalvarAsync();

        return entregue;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Guid>> ObterPendentesParaEnvioAsync(int limite)
    {
        var pendentes = await _repo.ObterPendentesAsync(DateTime.UtcNow, limite);

        return pendentes.Select(n => n.Id);
    }

    /// <summary>A caixa de entrada é do Responsável: o escopo vem do token (RN-106).</summary>
    private void GarantirEscopo(Guid tutorId)
    {
        if (_usuario.EhAdmin || _usuario.TutorId == tutorId)
            return;

        throw new AcessoNegadoException("RN-106", "Estas notificacoes nao pertencem ao seu escopo de acesso.");
    }

    private static NotificacaoDto Mapear(Notificacao n) => new()
    {
        Id = n.Id,
        TutorId = n.TutorId,
        Tipo = n.Tipo,
        Titulo = n.Titulo,
        Corpo = n.Corpo,
        Status = n.Status,
        AnimalId = n.AnimalId,
        ConsultaId = n.ConsultaId,
        Destino = n.Destino,
        AgendadaPara = n.AgendadaPara,
        EnviadaEm = n.EnviadaEm,
        LidaEm = n.LidaEm,
        CriadaEm = n.CriadaEm
    };
}
