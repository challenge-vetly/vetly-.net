using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.Domain.Entities;

/// <summary>
/// Representa um veterinário cadastrado na plataforma Vetly.
/// Pode atuar como autônomo ou vinculado a uma empresa.
/// </summary>
public class Veterinario
{
    /// <summary>Identificador único do veterinário (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Nome completo do veterinário.</summary>
    [Required(ErrorMessage = "O nome é obrigatório.")]
    [MaxLength(200, ErrorMessage = "O nome não pode ultrapassar 200 caracteres.")]
    public string Nome { get; private set; }

    /// <summary>
    /// CRMV do veterinário como value object.
    /// Valida o formato XXXXXX-UF antes de aceitar o valor.
    /// </summary>
    [Required(ErrorMessage = "O CRMV é obrigatório.")]
    public Crmv Crmv { get; private set; }

    /// <summary>
    /// Estado de atuação (UF) do veterinário.
    /// Armazenado em maiúsculo para facilitar buscas e comparações.
    /// </summary>
    [Required]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "A UF deve ter exatamente 2 caracteres.")]
    public string UfAtuacao { get; private set; }

    /// <summary>Lista de especialidades clínicas do veterinário (ex: Oncologia, Ortopedia).</summary>
    public List<string> Especialidades { get; private set; }

    /// <summary>Lista de espécies que o veterinário atende (ex: Canino, Felino).</summary>
    public List<string> EspeciesAtendidas { get; private set; }

    /// <summary>Titulação acadêmica opcional (ex: Doutor, Mestre).</summary>
    [MaxLength(300)]
    public string? TitulacaoAcademica { get; private set; }

    /// <summary>Indica se o veterinário é autônomo ou vinculado a uma empresa.</summary>
    [Required]
    public PersonaVeterinario Persona { get; private set; }

    /// <summary>Plano de assinatura ativo do veterinário na plataforma.</summary>
    [Required]
    public PlanoAssinatura Plano { get; private set; }

    /// <summary>Indica se o cadastro está ativo. Desativação é feita via soft delete.</summary>
    public bool Ativo { get; private set; }

    /// <summary>
    /// Id da empresa à qual o veterinário está vinculado.
    /// Nulo quando o veterinário é autônomo.
    /// </summary>
    public Guid? EmpresaId { get; private set; }

    private readonly List<StrikeReputacao> _strikesAtivos = [];

    /// <summary>
    /// Histórico de strikes de reputação (RN-065/066/067). Nunca apagado — a suspensão
    /// é decidida contando quantos caem na janela móvel de 90 dias a cada novo registro.
    /// </summary>
    public IReadOnlyCollection<StrikeReputacao> StrikesAtivos => _strikesAtivos.AsReadOnly();

    /// <summary>Enquanto no futuro, o perfil está suspenso do matching (RN-067).</summary>
    public DateTime? SuspensoAte { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core para materialização de entidades.</summary>
    private Veterinario()
    {
        Nome = null!;
        Crmv = null!;
        UfAtuacao = null!;
        Especialidades = [];
        EspeciesAtendidas = [];
    }

    /// <summary>
    /// Cria um novo veterinário com os dados obrigatórios.
    /// A lista de especialidades e espécies começa vazia e pode ser populada posteriormente.
    /// </summary>
    public Veterinario(string nome, Crmv crmv, string ufAtuacao, PersonaVeterinario persona, PlanoAssinatura plano)
    {
        Id = Guid.NewGuid();
        Nome = nome;
        Crmv = crmv;
        UfAtuacao = ufAtuacao.ToUpperInvariant(); // garante comparação case-insensitive no banco
        Persona = persona;
        Plano = plano;
        Especialidades = [];
        EspeciesAtendidas = [];
        Ativo = true;
    }

    /// <summary>Atualiza os dados cadastrais do veterinário.</summary>
    public void AtualizarDados(string nome, string ufAtuacao, string? titulacao)
    {
        Nome = nome;
        UfAtuacao = ufAtuacao.ToUpperInvariant();
        TitulacaoAcademica = titulacao;
    }

    /// <summary>Adiciona uma especialidade se ainda não estiver na lista.</summary>
    public void AdicionarEspecialidade(string especialidade)
    {
        if (!Especialidades.Contains(especialidade))
            Especialidades.Add(especialidade);
    }

    /// <summary>Adiciona uma espécie atendida se ainda não estiver na lista.</summary>
    public void AdicionarEspecie(string especie)
    {
        if (!EspeciesAtendidas.Contains(especie))
            EspeciesAtendidas.Add(especie);
    }

    /// <summary>
    /// Vincula o veterinário a uma empresa e altera a persona para Vinculado.
    /// RN-008: o veterinário autônomo pode ser vinculado a apenas uma empresa.
    /// </summary>
    public void VincularEmpresa(Guid empresaId)
    {
        EmpresaId = empresaId;
        Persona = PersonaVeterinario.Vinculado;
    }

    /// <summary>Desativa o cadastro (soft delete). Agendamentos futuros devem ser tratados pelo serviço.</summary>
    public void Desativar() => Ativo = false;

    /// <summary>Atualiza o plano de assinatura do veterinário.</summary>
    public void AtualizarPlano(PlanoAssinatura plano) => Plano = plano;

    /// <summary>
    /// Registra um strike de reputação (RN-065/066). Ao atingir 3 strikes dentro da
    /// janela móvel de 90 dias, suspende o perfil do matching por 7 dias (RN-067).
    /// O histórico completo é preservado — strikes fora da janela não são apagados,
    /// apenas deixam de contar para o limiar.
    /// </summary>
    public void RegistrarStrike(DateTime agora, string motivo)
    {
        _strikesAtivos.Add(new StrikeReputacao(agora, motivo));

        if (StrikesNaJanela(agora) >= 3)
            SuspensoAte = agora.AddDays(7);
    }

    /// <summary>Quantidade de strikes dentro da janela móvel de 90 dias, a partir de <paramref name="agora"/>.</summary>
    public int StrikesNaJanela(DateTime agora) =>
        _strikesAtivos.Count(s => (agora - s.Data).TotalDays <= 90);

    /// <summary>Indica se o perfil está suspenso do matching em <paramref name="agora"/> (RN-067).</summary>
    public bool EstaSuspenso(DateTime agora) => SuspensoAte is not null && agora <= SuspensoAte.Value;
}
