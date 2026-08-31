using System.Text.Json;
using Vetly.Application.DTOs.Captura;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Fecho documental da consulta (RN-082/RN-085, §7.3).
///
/// A IA sugere; quem decide é o veterinário, e a decisão fica registrada. São três
/// caminhos porque aprovar sem ler, corrigir antes de aprovar e recusar são coisas
/// diferentes — registrá-las como se fossem a mesma apagaria justamente o que a
/// auditoria precisa saber.
/// </summary>
public class ProntuarioService : IProntuarioService
{
    private readonly ICapturaRepository _captura;
    private readonly IConsultaRepository _consultaRepo;
    private readonly IAuditoriaIaRepository _auditoria;
    private readonly IUsuarioAtual _usuario;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public ProntuarioService(
        ICapturaRepository captura,
        IConsultaRepository consultaRepo,
        IAuditoriaIaRepository auditoria,
        IUsuarioAtual usuario)
    {
        _captura = captura;
        _consultaRepo = consultaRepo;
        _auditoria = auditoria;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public async Task<DecisaoRegistradaDto> DecidirAsync(Guid consultaId, DecisaoDoProntuarioDto dto)
    {
        var consulta = await ObterConsultaDoVeterinarioAsync(consultaId);

        var rascunho = await _captura.ObterRascunhoDaConsultaAsync(consultaId)
            ?? throw new NotFoundException("Rascunho da consulta", consultaId);

        if (!rascunho.AguardandoDecisao())
            throw new ConflitoDeEstadoException("RN-082",
                "Este rascunho ja foi decidido.");

        var conteudo = ValidarDecisao(dto, rascunho);

        // Decidido antes de qualquer efeito: o registro de auditoria e a decisao
        // precisam contar a mesma historia
        rascunho.RegistrarDecisao(dto.Decisao);

        var sessao = await _captura.ObterSessaoDaConsultaAsync(consultaId);

        if (dto.Decisao == DecisaoSobreRascunho.NaoAprovado)
        {
            // Recusa e desfecho legitimo: o ciclo encerra sem documentos e a consulta
            // segue pelo prontuario manual (RN-085). O diagnostico NAO fica validado,
            // e sem validacao nao se gera documento (RN-082).
            sessao?.EncerrarSemDocumentos();
        }
        else
        {
            consulta.ValidarDiagnostico();
            _consultaRepo.Atualizar(consulta);

            sessao?.IniciarDocumentacao();
        }

        if (sessao is not null)
            _captura.AtualizarSessao(sessao);

        var registro = new LogAuditoriaIa(
            consultaId,
            sessao?.Id,
            rascunho.Id,
            _usuario.VeterinarioId,
            dto.Decisao,
            dto.Decisao == DecisaoSobreRascunho.NaoAprovado ? string.Empty : Serializar(conteudo!),
            dto.Justificativa,
            dto.Decisao != DecisaoSobreRascunho.Aprovado,
            rascunho.Modelo);

        await _auditoria.AdicionarAsync(registro);

        await _captura.SalvarAsync();
        await _consultaRepo.SalvarAsync();
        await _auditoria.SalvarAsync();

        return new DecisaoRegistradaDto
        {
            ConsultaId = consultaId,
            LogAuditoriaId = registro.Id,
            Decisao = dto.Decisao,
            DiagnosticoValidado = consulta.DiagnosticoValidado,
            EstadoDaSessao = sessao?.Estado,
            RegistradoEm = registro.RegistradoEm
        };
    }

    /// <inheritdoc/>
    public async Task<DecisaoRegistradaDto> RegistrarManualAsync(Guid consultaId, ProntuarioManualDto dto)
    {
        var consulta = await ObterConsultaDoVeterinarioAsync(consultaId);

        if (EstaVazio(dto.Conteudo))
            throw new ValidationException("conteudo",
                "O prontuario manual precisa de conteudo clinico.");

        var rascunho = await _captura.ObterRascunhoDaConsultaAsync(consultaId);

        // O caminho manual existe para quando a IA nao esta no meio. Havendo rascunho
        // pendente, a decisao sobre ele vem primeiro — senao ficariam dois prontuarios
        // concorrentes sobre o mesmo atendimento (RN-082).
        if (rascunho is not null && rascunho.AguardandoDecisao())
            throw new ConflitoDeEstadoException("RN-082",
                "Ha um rascunho aguardando decisao. Decida sobre ele antes de escrever o prontuario a mao.");

        // Conteudo escrito pelo proprio veterinario ja e conteudo validado: nao ha
        // sugestao de IA a conferir (RN-085).
        consulta.ValidarDiagnostico();
        _consultaRepo.Atualizar(consulta);

        var sessao = await _captura.ObterSessaoDaConsultaAsync(consultaId);

        if (sessao is not null)
        {
            sessao.IniciarDocumentacao();
            _captura.AtualizarSessao(sessao);
        }

        var registro = new LogAuditoriaIa(
            consultaId,
            sessao?.Id,
            rascunhoIaId: null,
            _usuario.VeterinarioId,
            DecisaoSobreRascunho.Manual,
            Serializar(dto.Conteudo),
            justificativa: null,
            alterouSugestao: false,
            modelo: null);

        await _auditoria.AdicionarAsync(registro);

        await _captura.SalvarAsync();
        await _consultaRepo.SalvarAsync();
        await _auditoria.SalvarAsync();

        return new DecisaoRegistradaDto
        {
            ConsultaId = consultaId,
            LogAuditoriaId = registro.Id,
            Decisao = DecisaoSobreRascunho.Manual,
            DiagnosticoValidado = consulta.DiagnosticoValidado,
            EstadoDaSessao = sessao?.Estado,
            RegistradoEm = registro.RegistradoEm
        };
    }

    /// <inheritdoc/>
    public async Task<List<LogAuditoriaIaDto>> ObterAuditoriaAsync(Guid consultaId)
    {
        await ObterConsultaDoVeterinarioAsync(consultaId);

        var registros = await _auditoria.ObterDaConsultaAsync(consultaId);

        return [.. registros.Select(r => new LogAuditoriaIaDto
        {
            Id = r.Id,
            ConsultaId = r.ConsultaId,
            RascunhoIaId = r.RascunhoIaId,
            VeterinarioId = r.VeterinarioId,
            Decisao = r.Decisao,
            ConteudoFinal = r.ConteudoFinal,
            Justificativa = r.Justificativa,
            AlterouSugestao = r.AlterouSugestao,
            Modelo = r.Modelo,
            RegistradoEm = r.RegistradoEm
        })];
    }

    /// <summary>
    /// Cada caminho exige o que o torna auditável: corrigir sem dizer o que mudou não
    /// é corrigir, e recusar sem registrar o porquê deixa a trilha sem a informação
    /// que mais importa (RN-082).
    /// </summary>
    private static ConteudoDoProntuarioDto? ValidarDecisao(DecisaoDoProntuarioDto dto, RascunhoIa rascunho) =>
        dto.Decisao switch
        {
            DecisaoSobreRascunho.Aprovado => DoRascunho(rascunho),

            DecisaoSobreRascunho.Corrigido when dto.Correcao is null || EstaVazio(dto.Correcao) =>
                throw new ValidationException("correcao",
                    "A correcao precisa trazer o conteudo corrigido."),

            DecisaoSobreRascunho.Corrigido => dto.Correcao,

            DecisaoSobreRascunho.NaoAprovado when string.IsNullOrWhiteSpace(dto.Justificativa) =>
                throw new ValidationException("justificativa",
                    "Recusar o rascunho exige justificativa."),

            DecisaoSobreRascunho.NaoAprovado => null,

            _ => throw new ValidationException("decisao",
                "A decisao deve ser Aprovado, Corrigido ou NaoAprovado.")
        };

    private static ConteudoDoProntuarioDto DoRascunho(RascunhoIa rascunho) => new()
    {
        Anamnese = rascunho.Anamnese,
        ExameFisico = rascunho.ExameFisico,
        HipotesesDiagnosticas = [.. rascunho.HipotesesDiagnosticas],
        Conduta = rascunho.Conduta,
        Orientacoes = rascunho.Orientacoes
    };

    private static bool EstaVazio(ConteudoDoProntuarioDto conteudo) =>
        string.IsNullOrWhiteSpace(conteudo.Anamnese)
        && string.IsNullOrWhiteSpace(conteudo.ExameFisico)
        && string.IsNullOrWhiteSpace(conteudo.Conduta)
        && conteudo.HipotesesDiagnosticas.Count == 0;

    /// <summary>
    /// Guarda o conteúdo final inteiro, e não um diff: reconstruir o que foi aceito a
    /// partir de diferenças é frágil justamente quando mais importa.
    /// </summary>
    private static string Serializar(ConteudoDoProntuarioDto conteudo) =>
        JsonSerializer.Serialize(conteudo, Json);

    /// <summary>A consulta pertence ao veterinário que a conduziu (RN-105).</summary>
    private async Task<Consulta> ObterConsultaDoVeterinarioAsync(Guid consultaId)
    {
        var consulta = await _consultaRepo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        if (consulta.Cancelada)
            throw new BusinessRuleException("CONSULTA-003",
                "Nao e possivel validar diagnostico de consulta cancelada.");

        if (_usuario.EhAdmin || _usuario.VeterinarioId == consulta.VeterinarioId)
            return consulta;

        throw new AcessoNegadoException("RN-105", "Esta consulta nao pertence ao seu escopo de acesso.");
    }
}
