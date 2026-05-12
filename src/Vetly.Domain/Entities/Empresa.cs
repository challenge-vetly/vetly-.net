using System.ComponentModel.DataAnnotations;

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

    /// <summary>Construtor privado reservado ao EF Core para materialização de entidades.</summary>
    private Empresa()
    {
        Nome = null!;
        Tipo = null!;
    }

    /// <summary>Cria uma nova empresa com os dados obrigatórios.</summary>
    public Empresa(string nome, string tipo, Guid administradorId)
    {
        Id = Guid.NewGuid();
        Nome = nome;
        Tipo = tipo;
        AdministradorId = administradorId;
        Ativa = true;
    }

    /// <summary>Atualiza os dados cadastrais da empresa.</summary>
    public void AtualizarDados(string nome, string tipo)
    {
        Nome = nome;
        Tipo = tipo;
    }

    /// <summary>Altera o administrador responsável pela empresa.</summary>
    public void AlterarAdministrador(Guid novoAdminId) => AdministradorId = novoAdminId;

    /// <summary>Desativa a empresa (soft delete).</summary>
    public void Desativar() => Ativa = false;
}
