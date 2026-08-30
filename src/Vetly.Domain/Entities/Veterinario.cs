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

    // ── Endereço, matching e reputação ───────────────────────────────────────

    /// <summary>
    /// Endereço do veterinário, embutido na própria tabela (RN-026). Nulo apenas em
    /// cadastros anteriores à migration; é bloco obrigatório do cadastro novo.
    /// </summary>
    public Endereco? Endereco { get; private set; }

    /// <summary>Resultado da validação do CRMV junto ao conselho regional (RN-107).</summary>
    public StatusCrmv CrmvStatus { get; private set; }

    /// <summary>Data/hora da última resposta do conselho sobre o CRMV (RN-107).</summary>
    public DateTime? CrmvValidadoEm { get; private set; }

    /// <summary>
    /// Nota média das avaliações recebidas. Só é exibida e só entra no score de
    /// matching a partir de 3 avaliações (RN-057).
    /// </summary>
    public decimal NotaMedia { get; private set; }

    /// <summary>Quantidade de avaliações válidas recebidas (RN-057).</summary>
    public int NumAvaliacoes { get; private set; }

    /// <summary>Situação do perfil no motor de busca (RN-030 a RN-033).</summary>
    public StatusMatching MatchingStatus { get; private set; }

    /// <summary>
    /// Indica se o perfil está publicado no matching. Perfil com CRMV que não seja
    /// <see cref="StatusCrmv.Valido"/> nunca é publicado (RN-107).
    /// </summary>
    public bool Publicado { get; private set; }

    /// <summary>
    /// Data da publicação do perfil. Base do selo "Novo na Vetly", que vale
    /// por 30 dias a partir dela (RN-033).
    /// </summary>
    public DateTime? PublicadoEm { get; private set; }

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

        // Nunca se aprova CRMV por omissão: nasce pendente e só a validação junto ao
        // conselho promove o perfil a publicável (RN-107).
        CrmvStatus = StatusCrmv.PendenteValidacao;
        MatchingStatus = StatusMatching.Ativo;
        Publicado = false;
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
    /// Modelo relacional: o veterinário pertence a uma Empresa (N:1) quando vinculado (vetly-tech §2).
    /// </summary>
    public void VincularEmpresa(Guid empresaId)
    {
        EmpresaId = empresaId;
        Persona = PersonaVeterinario.Vinculado;
    }

    /// <summary>Define ou substitui o endereço do veterinário (RN-026).</summary>
    public void DefinirEndereco(Endereco endereco)
    {
        ArgumentNullException.ThrowIfNull(endereco);
        Endereco = endereco;
    }

    /// <summary>
    /// Registra a resposta do conselho sobre o CRMV (RN-107). Qualquer resultado
    /// diferente de <see cref="StatusCrmv.Valido"/> tira o perfil do matching.
    /// </summary>
    public void RegistrarValidacaoCrmv(StatusCrmv status, DateTime validadoEm)
    {
        CrmvStatus = status;
        CrmvValidadoEm = validadoEm;

        if (status != StatusCrmv.Valido)
        {
            Publicado = false;
            PublicadoEm = null;
        }
    }

    /// <summary>
    /// Publica o perfil no matching. Só é possível com CRMV válido (RN-107) — a
    /// publicação é idempotente e preserva a data original, de que depende o selo
    /// "Novo na Vetly" (RN-033).
    /// </summary>
    public bool PublicarNoMatching(DateTime publicadoEm)
    {
        if (CrmvStatus != StatusCrmv.Valido || !Ativo)
            return false;

        Publicado = true;
        PublicadoEm ??= publicadoEm;
        return true;
    }

    /// <summary>Remove o perfil do matching sem apagar o cadastro.</summary>
    public void RemoverDoMatching()
    {
        Publicado = false;
        MatchingStatus = StatusMatching.Suspenso;
    }

    /// <summary>
    /// Atualiza as métricas de reputação que alimentam o score do matching (RN-030/RN-057).
    /// </summary>
    public void AtualizarReputacao(decimal notaMedia, int numAvaliacoes)
    {
        if (notaMedia is < 0 or > 5)
            throw new ArgumentOutOfRangeException(nameof(notaMedia), "A nota média deve estar entre 0 e 5.");
        if (numAvaliacoes < 0)
            throw new ArgumentOutOfRangeException(nameof(numAvaliacoes), "O número de avaliações não pode ser negativo.");

        NotaMedia = notaMedia;
        NumAvaliacoes = numAvaliacoes;
    }

    /// <summary>
    /// Verdadeiro quando a nota já pode ser exibida publicamente e entrar no score:
    /// exige o mínimo de 3 avaliações (RN-057); abaixo disso vale a RN-033.
    /// </summary>
    public bool TemNotaPublica() => NumAvaliacoes >= 3;

    /// <summary>Desativa o cadastro (soft delete). Agendamentos futuros devem ser tratados pelo serviço.</summary>
    public void Desativar()
    {
        Ativo = false;
        // Vet desativado sai do matching junto — RN-022 encerra o acesso imediatamente
        Publicado = false;
    }

    /// <summary>Atualiza o plano de assinatura do veterinário.</summary>
    public void AtualizarPlano(PlanoAssinatura plano) => Plano = plano;
}
