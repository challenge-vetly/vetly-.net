using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

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
    /// Peso do animal em quilogramas. Nulo apenas em cadastros anteriores à migration —
    /// a API exige o peso na criação, porque sem ele a IA não pode sugerir dose (RN-081).
    /// </summary>
    public decimal? PesoKg { get; private set; }

    /// <summary>Sexo do animal. Nulo em cadastros anteriores à migration.</summary>
    public SexoAnimal? Sexo { get; private set; }

    /// <summary>Indica se o animal é castrado. Nulo quando a informação não foi coletada.</summary>
    public bool? Castrado { get; private set; }

    /// <summary>Id da mídia com a foto do animal, no storage de objetos. Nulo se não há foto.</summary>
    public Guid? FotoMidiaId { get; private set; }

    /// <summary>
    /// Alergias conhecidas do animal (ex: "Dipirona"). Alimentam os alertas de segurança,
    /// que nunca podem ser ocultados do histórico (RN-068).
    /// Persistida como string delimitada por ponto-e-vírgula no Oracle.
    /// </summary>
    public List<string> Alergias { get; private set; }

    /// <summary>
    /// Condições pré-existentes do animal (ex: "Displasia leve").
    /// Persistida como string delimitada por ponto-e-vírgula no Oracle.
    /// </summary>
    public List<string> CondicoesPreexistentes { get; private set; }

    /// <summary>
    /// Carteira de vacinação do animal. Persistida como JSON em coluna CLOB
    /// e usada para derivar o calendário de obrigações do pet (RN-046).
    /// </summary>
    public List<RegistroVacinacao> CarteiraVacinacao { get; private set; }

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
        Alergias = [];
        CondicoesPreexistentes = [];
        CarteiraVacinacao = [];
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
        Alergias = [];
        CondicoesPreexistentes = [];
        CarteiraVacinacao = [];
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

    /// <summary>
    /// Registra o peso do animal em quilogramas. Peso é pré-requisito para qualquer
    /// sugestão de dose pela IA (RN-081), por isso valores não positivos são recusados.
    /// </summary>
    public void RegistrarPeso(decimal pesoKg)
    {
        if (pesoKg <= 0)
            throw new ArgumentOutOfRangeException(nameof(pesoKg), "O peso deve ser maior que zero.");

        PesoKg = pesoKg;
    }

    /// <summary>
    /// Define o perfil clínico do animal (sexo, castração, foto, alergias e condições
    /// pré-existentes). Parâmetros nulos preservam o valor atual.
    /// </summary>
    public void DefinirPerfilClinico(
        SexoAnimal? sexo = null,
        bool? castrado = null,
        Guid? fotoMidiaId = null,
        IEnumerable<string>? alergias = null,
        IEnumerable<string>? condicoesPreexistentes = null)
    {
        if (sexo is not null) Sexo = sexo;
        if (castrado is not null) Castrado = castrado;
        if (fotoMidiaId is not null) FotoMidiaId = fotoMidiaId;
        if (alergias is not null) Alergias = [.. alergias];
        if (condicoesPreexistentes is not null) CondicoesPreexistentes = [.. condicoesPreexistentes];
    }

    /// <summary>Registra uma aplicação de vacina na carteira de vacinação do animal.</summary>
    public void RegistrarVacinacao(RegistroVacinacao registro)
    {
        ArgumentNullException.ThrowIfNull(registro);
        CarteiraVacinacao.Add(registro);
    }

    /// <summary>Substitui a carteira de vacinação inteira pelos registros informados.</summary>
    public void DefinirCarteiraVacinacao(IEnumerable<RegistroVacinacao> registros)
    {
        ArgumentNullException.ThrowIfNull(registros);
        CarteiraVacinacao = [.. registros];
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
