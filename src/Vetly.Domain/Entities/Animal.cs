using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;
using Vetly.Domain.Exceptions;

namespace Vetly.Domain.Entities;

/// <summary>
/// Representa um animal (paciente) cadastrado na plataforma Vetly.
/// Está sempre associado a um responsavel responsável.
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

    /// <summary>Id do responsavel responsável pelo animal. Chave estrangeira para TB_RESPONSAVEL.</summary>
    [Required]
    public Guid ResponsavelId { get; private set; }

    /// <summary>
    /// Lista de alertas clínicos ativos (ex: "Alergia a penicilina", "Epiléptico").
    /// Persistida como string delimitada por ponto-e-vírgula no Oracle.
    /// </summary>
    public List<string> AlertasAtivos { get; private set; }

    /// <summary>Indica se o cadastro está ativo. Desativação é feita via soft delete.</summary>
    public bool Ativo { get; private set; }

    /// <summary>Sexo do animal.</summary>
    public SexoAnimal Sexo { get; private set; }

    /// <summary>
    /// Peso em quilogramas. Obrigatório para a IA calcular dose de medicamento
    /// (RN-096.2) — pode ficar nulo até o primeiro registro/atualização.
    /// </summary>
    public decimal? PesoKg { get; private set; }

    /// <summary>Indica se o animal é castrado.</summary>
    public bool Castrado { get; private set; }

    /// <summary>Condições pré-existentes (ex: "Insuficiência renal crônica").</summary>
    public List<string> CondicoesPreExistentes { get; private set; }

    /// <summary>Alergias conhecidas (ex: "Penicilina").</summary>
    public List<string> Alergias { get; private set; }

    /// <summary>Carteira de vacinação — descrição simples de doses (ex: "V10 - 2026-03-10").</summary>
    public List<string> CarteiraVacinacao { get; private set; }

    /// <summary>Medicações atualmente em uso — cruzadas pela IA contra novas prescrições (RN-096.2).</summary>
    public List<string> MedicacoesEmUso { get; private set; }

    /// <summary>URL da foto do animal.</summary>
    [MaxLength(500)]
    public string? FotoUrl { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core para materialização de entidades.</summary>
    private Animal()
    {
        Nome = null!;
        Especie = null!;
        Raca = null!;
        AlertasAtivos = [];
        CondicoesPreExistentes = [];
        Alergias = [];
        CarteiraVacinacao = [];
        MedicacoesEmUso = [];
    }

    /// <summary>Cria um novo animal associado a um responsavel.</summary>
    public Animal(
        string nome, string especie, string raca, SexoAnimal sexo, DateTime dataNascimento,
        Guid responsavelId, bool castrado = false, decimal? pesoKg = null, string? fotoUrl = null)
    {
        if (pesoKg is <= 0)
            throw new DomainException("ANIMAL-001", "O peso do animal deve ser maior que zero.");

        Id = Guid.NewGuid();
        Nome = nome;
        Especie = especie;
        Raca = raca;
        Sexo = sexo;
        DataNascimento = dataNascimento;
        ResponsavelId = responsavelId;
        Castrado = castrado;
        PesoKg = pesoKg;
        FotoUrl = fotoUrl;
        AlertasAtivos = [];
        CondicoesPreExistentes = [];
        Alergias = [];
        CarteiraVacinacao = [];
        MedicacoesEmUso = [];
        Ativo = true;
    }

    /// <summary>Atualiza os dados cadastrais do animal.</summary>
    public void AtualizarDados(
        string nome, string especie, string raca, SexoAnimal sexo, DateTime dataNascimento,
        bool castrado, string? fotoUrl)
    {
        Nome = nome;
        Especie = especie;
        Raca = raca;
        Sexo = sexo;
        DataNascimento = dataNascimento;
        Castrado = castrado;
        FotoUrl = fotoUrl;
    }

    /// <summary>Substitui integralmente as listas de dados clínicos (condições, alergias, vacinação, medicações).</summary>
    public void AtualizarDadosClinicos(
        List<string> condicoesPreExistentes, List<string> alergias,
        List<string> carteiraVacinacao, List<string> medicacoesEmUso)
    {
        CondicoesPreExistentes = condicoesPreExistentes;
        Alergias = alergias;
        CarteiraVacinacao = carteiraVacinacao;
        MedicacoesEmUso = medicacoesEmUso;
    }

    /// <summary>Atualiza o peso do animal (RN-096.2). Exige valor maior que zero.</summary>
    public void AtualizarPeso(decimal pesoKg)
    {
        if (pesoKg <= 0)
            throw new DomainException("ANIMAL-001", "O peso do animal deve ser maior que zero.");

        PesoKg = pesoKg;
    }

    /// <summary>
    /// Oculta um prontuário da visão de veterinários que não o produziram (RN-088).
    /// <paramref name="prontuarioEhAlertaSeguranca"/> vem da classificação do próprio
    /// prontuário (<see cref="Prontuario.AlertaSeguranca"/>), consultada pelo Application
    /// antes de chamar este método — alertas de segurança nunca podem ser ocultados.
    /// </summary>
    public RegistroOcultado OcultarRegistro(Guid prontuarioId, bool prontuarioEhAlertaSeguranca, DateTime agora)
    {
        if (prontuarioEhAlertaSeguranca)
            throw new DomainException("ANIMAL-002",
                "Registros classificados como alerta de seguranca (alergias/interacoes) nunca podem ser ocultados.");

        return new RegistroOcultado(Id, prontuarioId, agora);
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
