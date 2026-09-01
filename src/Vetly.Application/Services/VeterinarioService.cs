using System.Text.RegularExpressions;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Consulta;
using Vetly.Application.DTOs.Veterinario;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.Application.Services;

/// <summary>
/// Servico de veterinarios. Orquestra validacoes de CRMV (RN-107),
/// soft delete com retorno de agendamentos (RN-022/RN-025) e gerenciamento de perfil.
/// </summary>
public class VeterinarioService : IVeterinarioService
{
    private static readonly Regex CrmvRegex = new(@"^\d{4,6}-[A-Z]{2}$", RegexOptions.Compiled);

    private readonly IVeterinarioRepository _repo;
    private readonly ICrmvAdapter _crmvAdapter;
    private readonly ISenhaHasher _hasher;
    private readonly IGeradorDeSenhaTemporaria _geradorDeSenha;
    private readonly IGeocodificacaoAdapter _geocodificacao;

    private readonly IConsultaRepository _consultaRepo;
    private readonly IPagamentoRepository _pagamentoRepo;
    private readonly IUsuarioAtual _usuario;

    public VeterinarioService(
        IVeterinarioRepository repo,
        ICrmvAdapter crmvAdapter,
        ISenhaHasher hasher,
        IGeradorDeSenhaTemporaria geradorDeSenha,
        IGeocodificacaoAdapter geocodificacao,
        IConsultaRepository consultaRepo,
        IPagamentoRepository pagamentoRepo,
        IUsuarioAtual usuario)
    {
        _repo = repo;
        _crmvAdapter = crmvAdapter;
        _hasher = hasher;
        _geradorDeSenha = geradorDeSenha;
        _geocodificacao = geocodificacao;
        _consultaRepo = consultaRepo;
        _pagamentoRepo = pagamentoRepo;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public async Task<ExtratoDoVeterinarioDto> ObterExtratoAsync(DateTime? inicio, DateTime? fim)
    {
        // O extrato e do proprio profissional, sempre: o escopo vem do token e nao ha
        // parametro de veterinario na rota — nem para o Admin, que tem o consolidado
        // da unidade por outro caminho (RN-105/RN-106).
        var vetId = _usuario.VeterinarioId
            ?? throw new AcessoNegadoException("RN-024",
                "O extrato e do proprio veterinario. Entre com um cadastro de veterinario.");

        var vet = await _repo.ObterPorIdAsync(vetId)
            ?? throw new NotFoundException("Veterinario", vetId);

        // Periodo padrao: os ultimos 12 meses. E o recorte que resolve conferencia de
        // repasse e fecho contabil sem obrigar o profissional a escolher datas.
        var fimDoPeriodo = fim ?? DateTime.UtcNow;
        var inicioDoPeriodo = inicio ?? fimDoPeriodo.AddMonths(-12);

        if (inicioDoPeriodo > fimDoPeriodo)
            throw new ValidationException("inicio", "O inicio do periodo nao pode ser depois do fim.");

        var consultas = await _consultaRepo.ObterPorVeterinarioAsync(vetId, inicioDoPeriodo, fimDoPeriodo);

        var itens = new List<ItemDoExtratoDto>();

        foreach (var consulta in consultas.OrderByDescending(c => c.DataHora))
        {
            var pagamento = await _pagamentoRepo.ObterPorConsultaAsync(consulta.Id);

            itens.Add(new ItemDoExtratoDto
            {
                ConsultaId = consulta.Id,
                DataDoAtendimento = consulta.EncerradaEm ?? consulta.DataHora,
                Modalidade = consulta.Modalidade,
                Status = consulta.Status,

                // Sem espécie, sem nome, sem diagnóstico: o extrato é financeiro, e
                // dado clínico aqui seria dado vazando por uma porta que a RN-022
                // fechou.
                Valor = pagamento?.Valor ?? 0m,
                Comissao = pagamento?.Comissao,
                Repasse = pagamento?.Repasse,
                PlanoAplicado = pagamento?.PlanoAplicado,
                StatusPagamento = pagamento?.StatusPagamento ?? consulta.StatusPagamento,
                Liquidado = pagamento?.Liquidado ?? false
            });
        }

        // So o que foi efetivamente cobrado entra na soma: consulta cancelada ou
        // expirada aparece na lista, para o profissional conferir, mas nao soma
        // dinheiro que nao existiu.
        var cobrados = itens.Where(i => i.StatusPagamento == StatusPagamento.Confirmado).ToList();

        return new ExtratoDoVeterinarioDto
        {
            VeterinarioId = vet.Id,
            Nome = vet.Nome,
            Crmv = vet.Crmv.Valor,
            CadastroAtivo = vet.Ativo,
            PeriodoInicio = inicioDoPeriodo,
            PeriodoFim = fimDoPeriodo,
            TotalDeAtendimentos = cobrados.Count,
            ValorBruto = cobrados.Sum(i => i.Valor),
            ComissaoDaPlataforma = cobrados.Sum(i => i.Comissao ?? 0m),
            RepasseTotal = cobrados.Sum(i => i.Repasse ?? 0m),
            RepasseLiquidado = cobrados.Where(i => i.Liquidado).Sum(i => i.Repasse ?? 0m),
            RepassePendente = cobrados.Where(i => !i.Liquidado).Sum(i => i.Repasse ?? 0m),
            Itens = itens
        };
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<VeterinarioDto>> ObterTodosAsync()
    {
        var vets = await _repo.ObterAtivosAsync();
        return vets.Select(MapearParaDto);
    }

    /// <inheritdoc/>
    public async Task<VeterinarioDto> ObterPorIdAsync(Guid id)
    {
        var vet = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Veterinario", id);
        return MapearParaDto(vet);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<VeterinarioDto>> ObterPorRegiaoAsync(string uf)
    {
        var vets = await _repo.ObterPorUfAsync(uf);
        return vets.Select(MapearParaDto);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ConsultaDto>> ObterAgendaAsync(Guid veterinarioId)
    {
        var consultas = await _repo.ObterAgendaFuturaAsync(veterinarioId);
        return consultas.Select(MapearConsultaParaDto);
    }

    /// <inheritdoc/>
    public async Task<VeterinarioCriadoDto> CriarAsync(CriarVeterinarioDto dto)
    {
        // RN-107: valida formato do CRMV antes de aceitar o cadastro
        ValidarCrmv(dto.Crmv);

        var existente = await _repo.ObterPorCrmvAsync(dto.Crmv);
        if (existente is not null)
            throw new BusinessRuleException("RN-107", $"CRMV '{dto.Crmv}' ja esta cadastrado na plataforma.");

        var emailExistente = await _repo.ObterPorEmailAsync(dto.Email);
        if (emailExistente is not null)
            throw new BusinessRuleException("VETERINARIO-001", "E-mail ja cadastrado na plataforma.");

        var crmv = new Crmv(dto.Crmv);
        var vet = new Veterinario(dto.Nome, crmv, dto.UfAtuacao, dto.Persona, dto.Plano);

        // Credencial de primeiro acesso (P-05): sem servico de e-mail no projeto, a senha
        // e devolvida ao Admin nesta resposta e so nela. Nasce marcada como temporaria.
        var senhaTemporaria = _geradorDeSenha.Gerar();
        vet.DefinirEmail(dto.Email);
        vet.DefinirSenhaHash(_hasher.GerarHash(senhaTemporaria), temporaria: true);

        if (dto.Endereco is not null)
            vet.DefinirEndereco(await MapearEnderecoGeocodificadoAsync(dto.Endereco));

        foreach (var esp in dto.Especialidades) vet.AdicionarEspecialidade(esp);
        foreach (var esp in dto.EspeciesAtendidas) vet.AdicionarEspecie(esp);
        if (dto.TitulacaoAcademica is not null)
            vet.AtualizarDados(dto.Nome, dto.UfAtuacao, dto.TitulacaoAcademica);

        // RN-107: o formato ja passou; agora vale o que o conselho responde. Perfil so e
        // publicado no matching com registro Valido — Indisponivel mantem pendente.
        await ValidarCrmvNoConselhoAsync(vet);

        await _repo.AdicionarAsync(vet);
        await _repo.SalvarAsync();

        return new VeterinarioCriadoDto
        {
            Veterinario = MapearParaDto(vet),
            SenhaTemporaria = senhaTemporaria,
            Email = vet.Email!
        };
    }

    /// <inheritdoc/>
    public async Task<ResultadoCrmvDto> RevalidarCrmvAsync(Guid id)
    {
        var vet = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Veterinario", id);

        var resultado = await ValidarCrmvNoConselhoAsync(vet);

        _repo.Atualizar(vet);
        await _repo.SalvarAsync();
        return resultado;
    }

    /// <inheritdoc/>
    public async Task<SituacaoCrmvDto> ObterSituacaoCrmvAsync(Guid id)
    {
        var vet = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Veterinario", id);

        return new SituacaoCrmvDto
        {
            VeterinarioId = vet.Id,
            Crmv = vet.Crmv.Valor,
            UfAtuacao = vet.UfAtuacao,
            Status = vet.CrmvStatus,
            ValidadoEm = vet.CrmvValidadoEm,
            Publicado = vet.Publicado,
            PublicadoEm = vet.PublicadoEm
        };
    }

    /// <summary>
    /// Consulta o conselho e aplica o resultado ao perfil (RN-107).
    /// <c>Indisponivel</c> nao aprova nem reprova: mantem o perfil em PendenteValidacao,
    /// fora do matching — a plataforma nunca aprova por omissao.
    /// </summary>
    private async Task<ResultadoCrmvDto> ValidarCrmvNoConselhoAsync(Veterinario vet)
    {
        var resultado = await _crmvAdapter.ValidarRegistroAsync(vet.Crmv.Valor, vet.UfAtuacao);

        var status = resultado.Resultado switch
        {
            ResultadoValidacaoCrmv.Valido => StatusCrmv.Valido,
            ResultadoValidacaoCrmv.Invalido => StatusCrmv.Invalido,
            ResultadoValidacaoCrmv.Suspenso => StatusCrmv.Suspenso,
            _ => StatusCrmv.PendenteValidacao
        };

        vet.RegistrarValidacaoCrmv(status, resultado.ConsultadoEm);

        if (status == StatusCrmv.Valido)
            vet.PublicarNoMatching(resultado.ConsultadoEm);

        return resultado;
    }

    /// <inheritdoc/>
    public async Task AtualizarAsync(Guid id, CriarVeterinarioDto dto)
    {
        var vet = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Veterinario", id);

        // RN-105: o profissional edita o proprio perfil; o Admin edita os da unidade.
        // Ninguem edita o perfil de um terceiro.
        var ehOProprio = _usuario.VeterinarioId == id;

        if (!_usuario.EhAdmin && !ehOProprio)
            throw new AcessoNegadoException("RN-105", "Este cadastro nao pertence ao seu escopo de acesso.");

        vet.AtualizarDados(dto.Nome, dto.UfAtuacao, dto.TitulacaoAcademica);

        // O plano decide o take rate (RN-070) e libera a IA na consulta (RN-085): e
        // decisao comercial da unidade, nao preferencia do profissional. Deixar o vet
        // trocar o proprio plano seria deixa-lo baixar a propria comissao.
        if (_usuario.EhAdmin)
            vet.AtualizarPlano(dto.Plano);

        if (dto.Endereco is not null)
            vet.DefinirEndereco(MapearEndereco(dto.Endereco));
        _repo.Atualizar(vet);
        await _repo.SalvarAsync();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ConsultaDto>> DesativarAsync(Guid id)
    {
        var vet = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Veterinario", id);

        // RN-025: retorna agendamentos futuros antes de desativar para o controller informar o cliente
        var agendamentos = await _repo.ObterAgendaFuturaAsync(id);
        vet.Desativar();
        _repo.Atualizar(vet);
        await _repo.SalvarAsync();
        return agendamentos.Select(MapearConsultaParaDto);
    }

    /// <summary>
    /// RN-107: valida o formato do CRMV com regex.
    /// Em producao, esta etapa seria seguida de consulta a API do CFMV.
    /// </summary>
    private static void ValidarCrmv(string crmv)
    {
        if (!CrmvRegex.IsMatch(crmv))
            throw new ValidationException("crmv", $"CRMV '{crmv}' esta em formato invalido. Use o padrao XXXXXX-UF.");
    }

    /// <summary>
    /// Converte o endereço do DTO em value object. Latitude/longitude do payload sao
    /// ignoradas de proposito: a coordenada e derivada do endereco pela geocodificacao,
    /// nunca informada pelo cliente (RN-026).
    /// </summary>
    private static Endereco MapearEndereco(EnderecoDto dto) =>
        new(dto.Cep, dto.Logradouro, dto.Numero, dto.Bairro, dto.Cidade, dto.Uf, dto.Complemento);

    /// <summary>
    /// Monta o endereco e deriva a coordenada dele pela geocodificacao (RN-026).
    /// Endereco que nao resolve fica sem coordenada — inventar posicao poria o
    /// prestador no lugar errado do mapa, o que e pior que nao aparecer na busca.
    /// </summary>
    private async Task<Endereco> MapearEnderecoGeocodificadoAsync(EnderecoDto dto)
    {
        var endereco = MapearEndereco(dto);
        var coordenada = await _geocodificacao.GeocodificarAsync(dto);

        if (coordenada.Resolvida)
            endereco.DefinirCoordenada(coordenada.Latitude!.Value, coordenada.Longitude!.Value, coordenada.Revisar);

        return endereco;
    }

    private static EnderecoDto? MapearEnderecoParaDto(Endereco? e) => e is null ? null : new EnderecoDto
    {
        Cep = e.Cep, Logradouro = e.Logradouro, Numero = e.Numero, Complemento = e.Complemento,
        Bairro = e.Bairro, Cidade = e.Cidade, Uf = e.Uf,
        Latitude = e.Latitude, Longitude = e.Longitude, CoordenadaRevisar = e.CoordenadaRevisar
    };

    private static VeterinarioDto MapearParaDto(Veterinario v) => new()
    {
        Id = v.Id, Nome = v.Nome, Crmv = v.Crmv.Valor, UfAtuacao = v.UfAtuacao,
        Especialidades = v.Especialidades, EspeciesAtendidas = v.EspeciesAtendidas,
        TitulacaoAcademica = v.TitulacaoAcademica, Persona = v.Persona,
        Plano = v.Plano, Ativo = v.Ativo, EmpresaId = v.EmpresaId,
        Endereco = MapearEnderecoParaDto(v.Endereco),
        CrmvStatus = v.CrmvStatus, CrmvValidadoEm = v.CrmvValidadoEm,
        NotaMedia = v.NotaMedia, NumAvaliacoes = v.NumAvaliacoes, NotaPublica = v.TemNotaPublica(),
        MatchingStatus = v.MatchingStatus, Publicado = v.Publicado, PublicadoEm = v.PublicadoEm
    };

    private static ConsultaDto MapearConsultaParaDto(Consulta c) => new()
    {
        Id = c.Id, DataHora = c.DataHora, Modalidade = c.Modalidade,
        VeterinarioId = c.VeterinarioId, AnimalId = c.AnimalId, TutorId = c.TutorId,
        DiagnosticoValidado = c.DiagnosticoValidado, ProtocoloValidado = c.ProtocoloValidado,
        StatusPagamento = c.StatusPagamento, Status = c.Status, Cancelada = c.Cancelada
    };
}
