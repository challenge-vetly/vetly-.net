using System.ComponentModel.DataAnnotations;

namespace Vetly.Domain.Entities;

/// <summary>
/// Representa um animal (paciente) cadastrado na plataforma Vetly.
/// Está sempre associado a um tutor responsável.
/// </summary>
public class Animal
{
    /// <summary>Identificador único do animal (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Nome do animal.</summary>
    [Required(ErrorMessage = "O nome do animal é obrigatório.")]
    [MaxLength(200)]
    public string Nome { get; private set; }

    /// <summary>Espécie do animal (ex: Canino, Felino, Ave).</summary>
    [Required(ErrorMessage = "A espécie é obrigatória.")]
    [MaxLength(100)]
    public string Especie { get; private set; }

    /// <summary>Raça do animal (ex: Golden Retriever, SRD).</summary>
    [Required(ErrorMessage = "A raça é obrigatória.")]
    [MaxLength(100)]
    public string Raca { get; private set; }

    /// <summary>Data de nascimento usada para calcular a idade e o protocolo clínico adequado.</summary>
    [Required]
    public DateTime DataNascimento { get; private set; }

    /// <summary>Id do tutor responsável pelo animal. Chave estrangeira para TB_TUTOR.</summary>
    [Required]
    public Guid TutorId { get; private set; }

    /// <summary>
    /// Lista de alertas clínicos ativos (ex: "Alergia a penicilina", "Epiléptico").
    /// Persistida como string delimitada por ponto-e-vírgula no Oracle.
    /// </summary>
    public List<string> AlertasAtivos { get; private set; }

    /// <summary>Indica se o cadastro está ativo. Desativação é feita via soft delete.</summary>
    public bool Ativo { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core para materialização de entidades.</summary>
    private Animal()
    {
        Nome = null!;
        Especie = null!;
        Raca = null!;
        AlertasAtivos = [];
    }

    /// <summary>Cria um novo animal associado a um tutor.</summary>
    public Animal(string nome, string especie, string raca, DateTime dataNascimento, Guid tutorId)
    {
        Id = Guid.NewGuid();
        Nome = nome;
        Especie = especie;
        Raca = raca;
        DataNascimento = dataNascimento;
        TutorId = tutorId;
        AlertasAtivos = [];
        Ativo = true;
    }

    /// <summary>Atualiza os dados cadastrais do animal.</summary>
    public void AtualizarDados(string nome, string especie, string raca, DateTime dataNascimento)
    {
        Nome = nome;
        Especie = especie;
        Raca = raca;
        DataNascimento = dataNascimento;
    }

    /// <summary>Adiciona um alerta clínico se ainda não estiver registrado.</summary>
    public void AdicionarAlerta(string alerta)
    {
        if (!AlertasAtivos.Contains(alerta))
            AlertasAtivos.Add(alerta);
    }

    /// <summary>Remove um alerta clínico da lista ativa.</summary>
    public void RemoverAlerta(string alerta) => AlertasAtivos.Remove(alerta);

    /// <summary>Desativa o cadastro do animal (soft delete).</summary>
    public void Desativar() => Ativo = false;

    /// <summary>
    /// Calcula a idade em anos completos com base na data de nascimento.
    /// Usa UTC para evitar discrepâncias de fuso horário.
    /// </summary>
    public int IdadeEmAnos() => (int)((DateTime.UtcNow - DataNascimento).TotalDays / 365.25);
}
