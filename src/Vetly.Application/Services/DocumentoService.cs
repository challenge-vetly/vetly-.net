using Vetly.Application.DTOs.Notificacao;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Vetly.Application.DTOs.Captura;
using Vetly.Application.DTOs.Documento;
using Vetly.Application.Exceptions;
using Vetly.Application.Factories;
using Vetly.Application.Interfaces;
using Vetly.Application.Observability;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Serviço de documentos clínicos.
///
/// Usa Factory Pattern para selecionar a factory correta pelo tipo. A geração parte
/// do <b>estado final aprovado pelo veterinário</b> (RN-082/RN-083): o conteúdo vem
/// da trilha de auditoria, que é o registro do que o profissional de fato aceitou —
/// não do rascunho da IA nem de nova inferência.
/// </summary>
public class DocumentoService : IDocumentoService
{
    private readonly IDocumentoRepository _repo;
    private readonly IConsultaRepository _consultaRepo;
    private readonly IVeterinarioRepository _vetRepo;
    private readonly IAnimalRepository _animalRepo;
    private readonly ITutorRepository _tutorRepo;
    private readonly IPagamentoRepository _pagamentoRepo;
    private readonly IAuditoriaIaRepository _auditoria;
    private readonly IMidiaRepository _midiaRepo;
    private readonly INotificacaoService _notificacoes;
    private readonly IStorageAdapter _storage;
    private readonly IGeradorDePdf _pdf;
    private readonly IAssinaturaAdapter _assinatura;
    private readonly IColmeiaService _colmeia;
    private readonly ILogger<DocumentoService> _logger;
    private readonly IUsuarioAtual _usuario;
    private readonly IEnumerable<IDocumentoFactory> _factories;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public DocumentoService(
        IDocumentoRepository repo,
        IConsultaRepository consultaRepo,
        IVeterinarioRepository vetRepo,
        IAnimalRepository animalRepo,
        ITutorRepository tutorRepo,
        IPagamentoRepository pagamentoRepo,
        IAuditoriaIaRepository auditoria,
        IMidiaRepository midiaRepo,
        INotificacaoService notificacoes,
        IStorageAdapter storage,
        IGeradorDePdf pdf,
        IAssinaturaAdapter assinatura,
        IColmeiaService colmeia,
        ILogger<DocumentoService> logger,
        IUsuarioAtual usuario,
        IEnumerable<IDocumentoFactory> factories)
    {
        _repo = repo;
        _consultaRepo = consultaRepo;
        _vetRepo = vetRepo;
        _animalRepo = animalRepo;
        _tutorRepo = tutorRepo;
        _pagamentoRepo = pagamentoRepo;
        _auditoria = auditoria;
        _midiaRepo = midiaRepo;
        _notificacoes = notificacoes;
        _storage = storage;
        _pdf = pdf;
        _assinatura = assinatura;
        _colmeia = colmeia;
        _logger = logger;
        _usuario = usuario;
        _factories = factories;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<DocumentoDto>> ObterPorConsultaAsync(Guid consultaId)
    {
        var consulta = await _consultaRepo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        await GarantirLeituraAsync(consulta);

        var docs = await _repo.ObterPorConsultaAsync(consultaId);

        return docs.Select(MapearParaDto);
    }

    /// <inheritdoc/>
    public async Task<DocumentoDto> ObterPorIdAsync(Guid id)
    {
        var doc = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Documento", id);

        if (doc.ConsultaId is { } consultaId)
        {
            var consulta = await _consultaRepo.ObterPorIdAsync(consultaId);

            if (consulta is not null)
                await GarantirLeituraAsync(consulta);
        }

        return MapearParaDto(doc);
    }

    /// <summary>
    /// Quem le um documento clinico (RN-090/RN-105/RN-106): o Responsavel dono do
    /// animal, o veterinario que conduziu o atendimento, o Admin, e o veterinario de
    /// fora com colmeia vigente no escopo de documentos.
    ///
    /// Toda leitura por veterinario vira entrada no log de acesso (RN-067) — inclusive
    /// a do proprio autor. E o registro que sustenta a colmeia juridicamente, e
    /// registrar so o acesso "de fora" deixaria metade da historia fora.
    /// </summary>
    private async Task GarantirLeituraAsync(Consulta consulta)
    {
        if (_usuario.EhAdmin)
            return;

        if (_usuario.EhTutor && _usuario.TutorId == consulta.TutorId)
            return;

        if (_usuario.EhVeterinario && _usuario.VeterinarioId is { } vetId)
        {
            var doProprioAtendimento = vetId == consulta.VeterinarioId;

            var autorizado = doProprioAtendimento || await _colmeia.PodeAcessarAsync(
                vetId, consulta.AnimalId, EscopoAcessoColmeia.Documentos);

            await _colmeia.RegistrarAcessoAsync(
                consulta.AnimalId, EscopoAcessoColmeia.Documentos, autorizado,
                "GET /api/documentos");

            if (autorizado)
                return;
        }

        throw new AcessoNegadoException("RN-105", "Este documento nao pertence ao seu escopo de acesso.");
    }

    /// <summary>
    /// Quem emite, publica ou corrige documento e quem assina por ele: o veterinario
    /// do atendimento, ou o Admin (RN-083/RN-087/RN-105).
    /// </summary>
    private async Task<Consulta> GarantirEscritaAsync(Guid consultaId)
    {
        var consulta = await _consultaRepo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        if (_usuario.EhAdmin || _usuario.VeterinarioId == consulta.VeterinarioId)
            return consulta;

        throw new AcessoNegadoException("RN-105",
            "Somente o veterinario que conduziu o atendimento emite seus documentos.");
    }

    /// <inheritdoc/>
    public async Task<DocumentoDto> GerarAsync(Guid consultaId, TipoDocumento tipo, TipoAtestado? subtipo = null)
    {
        var consulta = await GarantirEscritaAsync(consultaId);

        if (!consulta.PodeGerarDocumentos())
            throw new BusinessRuleException("RN-082",
                "O diagnostico deve ser validado pelo veterinario antes de gerar documentos.");

        var factory = _factories.FirstOrDefault(f => f.TipoSuportado == tipo)
            ?? throw new InvalidOperationException($"Nenhuma factory registrada para o tipo '{tipo}'.");

        // Span de dominio: gerar documento encadeia leitura da trilha de auditoria da
        // IA, montagem do conteudo e renderizacao de PDF. Quando a rota fica lenta, e
        // este span que diz qual das tres etapas custou.
        using var atividade = VetlyTelemetry.Iniciar("documento.gerar");
        atividade?.SetTag("vetly.documento.tipo", tipo.ToString());
        atividade?.SetTag("vetly.consulta_id", consultaId);

        var contexto = await MontarContextoAsync(consulta, subtipo);

        var documento = factory.Criar(contexto);

        // O PDF é o que o Responsável leva para fora do app (RN-090). Falha ao
        // renderizar não perde o documento: o conteúdo já está gravado e o PDF pode
        // ser produzido depois.
        await AnexarPdfAsync(documento, contexto);

        await _repo.AdicionarAsync(documento);
        await _repo.SalvarAsync();

        // Producao documental por tipo (RN-083). E a metrica que mostra se a promessa
        // do produto — a consulta sai com prontuario, receita e NF prontos — esta de
        // fato acontecendo, ou se so o prontuario esta sendo emitido.
        VetlyTelemetry.DocumentosEmitidos.Add(1,
            new KeyValuePair<string, object?>("tipo", tipo.ToString()));

        return MapearParaDto(documento);
    }

    /// <inheritdoc/>
    public async Task<DocumentoDto> AssinarAsync(Guid id, string? nomeCompleto)
    {
        var doc = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Documento", id);

        if (doc.AssinadoDigitalmente)
            throw new ConflitoDeEstadoException("RN-087", "Este documento ja foi assinado.");

        var vet = await ObterSignatarioAsync(doc);

        var assinatura = await _assinatura.AssinarAsync(new SolicitacaoDeAssinaturaDto(
            doc.Id,
            doc.TipoDocumento.ToString(),
            vet.Nome,
            vet.Crmv.Valor,
            vet.UfAtuacao,
            nomeCompleto));

        doc.RegistrarAssinatura(assinatura.Metodo, assinatura.Carimbo);

        // O carimbo entra no corpo do documento: quem recebe precisa ver como foi
        // assinado sem perguntar (RN-087)
        if (!string.IsNullOrWhiteSpace(doc.Conteudo))
            doc.RegistrarConteudo($"{doc.Conteudo}\n\n{assinatura.Carimbo}");

        _repo.Atualizar(doc);
        await _repo.SalvarAsync();

        return MapearParaDto(doc);
    }

    /// <summary>
    /// Quem assina é o veterinário que conduziu o atendimento (RN-087/RN-105).
    /// Assinar documento de consulta alheia é exatamente o que esta guarda impede.
    /// </summary>
    private async Task<Veterinario> ObterSignatarioAsync(Documento doc)
    {
        if (doc.ConsultaId is not { } consultaId)
            throw new BusinessRuleException("RN-087",
                "Documento sem consulta vinculada nao tem signatario definido.");

        var consulta = await _consultaRepo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        if (!_usuario.EhAdmin && _usuario.VeterinarioId != consulta.VeterinarioId)
            throw new AcessoNegadoException("RN-105",
                "Somente o veterinario que conduziu o atendimento pode assinar seus documentos.");

        return await _vetRepo.ObterPorIdAsync(consulta.VeterinarioId)
            ?? throw new NotFoundException("Veterinario", consulta.VeterinarioId);
    }

    /// <inheritdoc/>
    public async Task<DocumentoDto> CorrigirAsync(
        Guid id, string novosDados, string? justificativa, string crmvSolicitante)
    {
        var doc = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Documento", id);

        if (doc.ConsultaId is { } consultaDaCorrecao)
            await GarantirEscritaAsync(consultaDaCorrecao);

        var horasDesdeGeracao = (DateTime.UtcNow - doc.DataGeracao).TotalHours;

        if (horasDesdeGeracao > 24 && string.IsNullOrWhiteSpace(justificativa))
            throw new BusinessRuleException("RN-089", "Correcoes apos 24h exigem justificativa.");

        var corrigido = new Documento(doc.TipoDocumento, crmvSolicitante, doc.ConsultaId, doc.InternacaoId);
        corrigido.Corrigir(doc.Id, DateTime.UtcNow, crmvSolicitante);

        // O documento original permanece intacto (RN-088): a correção é outra versão,
        // e o conteúdo novo entra aqui em vez de sobrescrever o que já foi publicado.
        if (!string.IsNullOrWhiteSpace(novosDados))
            corrigido.RegistrarConteudo(novosDados);
        else if (!string.IsNullOrWhiteSpace(doc.Conteudo))
            corrigido.RegistrarConteudo(doc.Conteudo);

        await _repo.AdicionarAsync(corrigido);
        await _repo.SalvarAsync();

        return MapearParaDto(corrigido);
    }

    /// <inheritdoc/>
    public async Task<DocumentoDto> PublicarAsync(Guid id)
    {
        var doc = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Documento", id);

        var consulta = doc.ConsultaId is { } cid ? await GarantirEscritaAsync(cid) : null;

        if (string.IsNullOrWhiteSpace(doc.Conteudo))
            throw new BusinessRuleException("RN-090",
                "Documento sem conteudo nao pode ser publicado no board do pet.");

        // RN-087: receita chega ao Responsável assinada, ou não chega. Documento
        // clínico sem assinatura no board pareceria válido sem ser.
        if (doc.TipoDocumento == TipoDocumento.ReceitaVeterinaria && !doc.AssinadoDigitalmente)
            throw new BusinessRuleException("RN-087",
                "A receita precisa estar assinada antes de ser publicada.");

        doc.Publicar(DateTime.UtcNow);
        _repo.Atualizar(doc);
        await _repo.SalvarAsync();

        // RN-011/RN-090/RN-091: publicar sem avisar deixaria o documento no board de
        // alguem que nao sabe que ele chegou. O push e o que fecha o ciclo.
        if (consulta is not null)
        {
            var animal = await _animalRepo.ObterPorIdAsync(consulta.AnimalId);

            await _notificacoes.CriarAsync(new CriarNotificacaoDto
            {
                TutorId = consulta.TutorId,
                Tipo = TipoNotificacao.DocumentoPublicado,
                Titulo = "Novo documento disponivel",
                Corpo = $"{doc.TipoDocumento} de {animal?.Nome ?? "seu pet"} ja esta no board.",
                AnimalId = consulta.AnimalId,
                ConsultaId = consulta.Id,
                Destino = $"/animais/{consulta.AnimalId}/documentos"
            });
        }

        return MapearParaDto(doc);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<DocumentoDto>> ObterDoBoardDoPetAsync(Guid animalId)
    {
        var animal = await _animalRepo.ObterPorIdAsync(animalId)
            ?? throw new NotFoundException("Animal", animalId);

        // RN-105/RN-106: o board é do pet do Responsável; o veterinário alcança apenas
        // os animais que atende, e o escopo vem do token, nunca do parâmetro.
        if (_usuario.EhTutor && _usuario.TutorId != animal.TutorId)
            throw new AcessoNegadoException("RN-105", "Este animal nao pertence ao seu escopo de acesso.");

        if (_usuario.EhVeterinario && _usuario.VeterinarioId is { } vetId
            && !await _animalRepo.VeterinarioAtendeAnimalAsync(vetId, animalId))
        {
            // Colmeia: o Responsavel pode ter autorizado este veterinario a ver os
            // documentos do animal, e o acesso fica registrado de todo jeito (RN-090).
            var autorizado = await _colmeia.PodeAcessarAsync(
                vetId, animalId, EscopoAcessoColmeia.Documentos);

            await _colmeia.RegistrarAcessoAsync(
                animalId, EscopoAcessoColmeia.Documentos, autorizado, "GET /api/documentos/animal");

            if (!autorizado)
                throw new AcessoNegadoException("RN-105", "Este animal nao pertence ao seu escopo de acesso.");
        }

        var documentos = await _repo.ObterPublicadosPorAnimalAsync(animalId);

        return documentos.Select(MapearParaDto);
    }

    /// <inheritdoc/>
    public async Task<DocumentoDto> MarcarComoLidoAsync(Guid id)
    {
        var doc = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Documento", id);

        // Quem marca como lido e quem leu: o Responsavel. O veterinario marcando
        // pelo app dele produziria um dado falso — "a orientacao chegou" quando nao
        // chegou a ninguem.
        if (doc.ConsultaId is { } consultaDaLeitura)
        {
            var consulta = await _consultaRepo.ObterPorIdAsync(consultaDaLeitura);

            if (consulta is not null && !_usuario.EhAdmin && _usuario.TutorId != consulta.TutorId)
                throw new AcessoNegadoException("RN-106", "Este documento nao pertence ao seu escopo de acesso.");
        }

        if (doc.PublicadoEm is null)
            throw new BusinessRuleException("RN-090",
                "Documento ainda nao publicado no board do pet.");

        doc.MarcarComoLido(DateTime.UtcNow);
        _repo.Atualizar(doc);
        await _repo.SalvarAsync();

        return MapearParaDto(doc);
    }

    /// <summary>
    /// Renderiza o PDF e o guarda no storage, registrando a mídia (RN-090).
    ///
    /// O PDF entra pelo mesmo registro de mídia dos outros arquivos: assim a URL é
    /// sempre temporária e emitida sob autorização, e conteúdo clínico nunca vira
    /// endereço público e permanente.
    /// </summary>
    private async Task AnexarPdfAsync(Documento documento, ContextoDoDocumentoDto contexto)
    {
        // Falha ao renderizar nao perde o documento: o conteudo clinico ja esta
        // gravado, e o PDF pode ser produzido depois. Derrubar a geracao inteira por
        // causa do anexo faria o veterinario perder o atendimento que acabou de
        // aprovar.
        try
        {
            var midia = new Midia(TipoMidia.DocumentoPdf, "application/pdf", consultaId: contexto.ConsultaId);

            var bytes = _pdf.Renderizar($"{documento.TipoDocumento} - {contexto.AnimalNome}", documento.Conteudo!);

            await _storage.GravarAsync(midia.ChaveStorage, bytes, "application/pdf");

            midia.ConfirmarUpload(bytes.LongLength);

            await _midiaRepo.AdicionarAsync(midia);
            await _midiaRepo.SalvarAsync();

            documento.AnexarPdf(midia.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Falha ao renderizar o PDF do documento {TipoDocumento} da consulta {ConsultaId}. " +
                "O documento segue sem PDF anexado.",
                documento.TipoDocumento, contexto.ConsultaId);
        }
    }

    /// <summary>
    /// Monta o contexto do documento. O conteúdo clínico vem do último registro de
    /// auditoria com conteúdo aceito — é o estado final aprovado pelo veterinário
    /// (RN-082/RN-083), e não o rascunho da IA.
    /// </summary>
    private async Task<ContextoDoDocumentoDto> MontarContextoAsync(Consulta consulta, TipoAtestado? subtipo)
    {
        var vet = await _vetRepo.ObterPorIdAsync(consulta.VeterinarioId)
            ?? throw new NotFoundException("Veterinario", consulta.VeterinarioId);

        var animal = await _animalRepo.ObterPorIdAsync(consulta.AnimalId)
            ?? throw new NotFoundException("Animal", consulta.AnimalId);

        var tutor = await _tutorRepo.ObterPorIdAsync(consulta.TutorId);
        var pagamento = await _pagamentoRepo.ObterPorConsultaAsync(consulta.Id);

        return new ContextoDoDocumentoDto
        {
            ConsultaId = consulta.Id,
            DataDoAtendimento = consulta.EncerradaEm ?? consulta.DataHora,
            Modalidade = consulta.Modalidade,
            VeterinarioNome = vet.Nome,
            Crmv = vet.Crmv.Valor,
            UfAtuacao = vet.UfAtuacao,
            AnimalNome = animal.Nome,
            Especie = animal.Especie,
            Raca = animal.Raca,
            DataNascimento = animal.DataNascimento,
            PesoKg = animal.PesoKg,
            Sexo = animal.Sexo?.ToString(),
            TutorNome = tutor?.Nome ?? "Nao informado",
            ValorDoAtendimento = pagamento?.Valor,
            SubtipoAtestado = subtipo,
            Conteudo = await ObterConteudoAprovadoAsync(consulta.Id)
        };
    }

    /// <summary>
    /// O que o veterinário aceitou, lido da trilha de auditoria (RN-082).
    ///
    /// Recusa e prontuário manual convivem na mesma consulta, então vale o registro
    /// mais recente que de fato tem conteúdo — a recusa grava vazio de propósito.
    /// </summary>
    private async Task<ConteudoDoProntuarioDto> ObterConteudoAprovadoAsync(Guid consultaId)
    {
        var trilha = await _auditoria.ObterDaConsultaAsync(consultaId);

        var aceito = trilha
            .Where(r => r.Decisao != DecisaoSobreRascunho.NaoAprovado
                        && !string.IsNullOrWhiteSpace(r.ConteudoFinal))
            .OrderByDescending(r => r.RegistradoEm)
            .FirstOrDefault();

        if (aceito is null)
            throw new BusinessRuleException("RN-083",
                "Nao ha conteudo clinico aprovado para esta consulta. " +
                "Decida sobre o rascunho ou registre o prontuario manual antes de gerar documentos.");

        return JsonSerializer.Deserialize<ConteudoDoProntuarioDto>(aceito.ConteudoFinal, Json)
            ?? throw new BusinessRuleException("RN-083",
                "O conteudo aprovado desta consulta nao pode ser lido.");
    }

    private static DocumentoDto MapearParaDto(Documento d) => new()
    {
        Id = d.Id,
        ConsultaId = d.ConsultaId,
        InternacaoId = d.InternacaoId,
        TipoDocumento = d.TipoDocumento,
        Versao = d.Versao,
        DataGeracao = d.DataGeracao,
        CrmvSignatario = d.CrmvSignatario,
        AssinadoDigitalmente = d.AssinadoDigitalmente,
        VersaoOriginalId = d.VersaoOriginalId,
        DataCorrecao = d.DataCorrecao,
        CrmvSolicitanteCorrecao = d.CrmvSolicitanteCorrecao,
        Conteudo = d.Conteudo,
        PdfMidiaId = d.PdfMidiaId,
        Subtipo = d.Subtipo,
        AssinaturaMetodo = d.AssinaturaMetodo,
        AssinaturaCarimbo = d.AssinaturaCarimbo,
        PublicadoEm = d.PublicadoEm,
        LidoEm = d.LidoEm
    };
}
