using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.Domain.Entities;

/// <summary>
/// Representa uma empresa (clínica ou hospital veterinário) cadastrada na plataforma Vetly.
/// Pode ter múltiplos veterinários vinculados e um administrador responsável.
/// </summary>
public class Empresa
{
    /// <summary>Identificador único da empresa (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Razão social ou nome fantasia da empresa.</summary>
    [Required(ErrorMessage = "O nome da empresa é obrigatório.")]
    [MaxLength(300)]
    public string Nome { get; private set; }

    /// <summary>Tipo da empresa (ex: Clínica, Hospital, Centro de Especialidades).</summary>
    [Required(ErrorMessage = "O tipo da empresa é obrigatório.")]
    [MaxLength(100)]
    public string Tipo { get; private set; }

    /// <summary>
    /// Id do veterinário administrador da empresa.
    /// O administrador tem permissões de gestão sobre os veterinários vinculados.
    /// </summary>
    [Required]
    public Guid AdministradorId { get; private set; }

    /// <summary>Indica se a empresa está ativa na plataforma.</summary>
    public bool Ativa { get; private set; }

    /// <summary>
    /// Endereço da unidade, embutido no próprio registro, nos mesmos moldes do
    /// veterinário (RN-026). A coordenada é derivada dele e alimenta a busca.
    /// </summary>
    public Endereco? Endereco { get; private set; }

    /// <summary>
    /// Percentual retido pela clínica no cancelamento da faixa parcial (24h–2h).
    /// A política é da clínica; a plataforma só a aplica e a torna transparente ao
    /// Responsável no momento do agendamento (RN-042).
    /// </summary>
    public decimal PercentualRetencaoParcial { get; private set; }

    /// <summary>Plano de assinatura da unidade, que define o take rate (RN-070/RN-072).</summary>
    public PlanoAssinatura Plano { get; private set; }

    /// <summary>
    /// Faixa Enterprise vigente, derivada do número de vets vinculados (RN-072).
    /// Nula quando o plano não é Enterprise.
    /// </summary>
    public FaixaEnterprise? FaixaEnterprise { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core para materialização de entidades.</summary>
    private Empresa()
    {
        Nome = null!;
        Tipo = null!;
    }

    /// <summary>Percentual de retenção adotado quando a clínica não configura o seu (RN-042).</summary>
    public const decimal PercentualRetencaoPadrao = 30m;

    /// <summary>Cria uma nova empresa com os dados obrigatórios.</summary>
    public Empresa(string nome, string tipo, Guid administradorId, PlanoAssinatura plano = PlanoAssinatura.Basico)
    {
        Id = Guid.NewGuid();
        Nome = nome;
        Tipo = tipo;
        AdministradorId = administradorId;
        Ativa = true;
        Plano = plano;
        PercentualRetencaoParcial = PercentualRetencaoPadrao;
    }

    /// <summary>Atualiza os dados cadastrais da empresa.</summary>
    public void AtualizarDados(string nome, string tipo)
    {
        Nome = nome;
        Tipo = tipo;
    }

    /// <summary>Altera o administrador responsável pela empresa.</summary>
    public void AlterarAdministrador(Guid novoAdminId) => AdministradorId = novoAdminId;

    /// <summary>Define ou substitui o endereço da unidade (RN-026).</summary>
    public void DefinirEndereco(Endereco endereco)
    {
        ArgumentNullException.ThrowIfNull(endereco);
        Endereco = endereco;
    }

    /// <summary>
    /// Configura a política de retenção do cancelamento parcial (RN-042).
    /// O percentual é exibido ao Responsável no agendamento, então precisa ser um
    /// percentual de verdade: entre 0 e 100.
    /// </summary>
    public void DefinirPoliticaRetencao(decimal percentual)
    {
        if (percentual is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(percentual),
                "O percentual de retenção deve estar entre 0 e 100.");

        PercentualRetencaoParcial = percentual;
    }

    /// <summary>
    /// Atualiza o plano da unidade. Sair do Enterprise limpa a faixa, que só existe
    /// dentro dele (RN-072).
    /// </summary>
    public void AtualizarPlano(PlanoAssinatura plano)
    {
        Plano = plano;
        if (plano != PlanoAssinatura.Enterprise)
            FaixaEnterprise = null;
    }

    /// <summary>
    /// Recalcula a faixa Enterprise a partir do número de veterinários vinculados.
    /// A troca de faixa ao cruzar o limite é automática (RN-072); fora do Enterprise
    /// não há faixa.
    /// </summary>
    public FaixaEnterprise? RecalcularFaixaEnterprise(int quantidadeDeVets)
    {
        if (quantidadeDeVets < 0)
            throw new ArgumentOutOfRangeException(nameof(quantidadeDeVets),
                "A quantidade de veterinários não pode ser negativa.");

        FaixaEnterprise = Plano != PlanoAssinatura.Enterprise
            ? null
            : quantidadeDeVets switch
            {
                <= 5 => Enums.FaixaEnterprise.De1a5,
                <= 10 => Enums.FaixaEnterprise.De6a10,
                <= 20 => Enums.FaixaEnterprise.De11a20,
                _ => Enums.FaixaEnterprise.Acima20
            };

        return FaixaEnterprise;
    }

    /// <summary>Desativa a empresa (soft delete).</summary>
    public void Desativar() => Ativa = false;
}
