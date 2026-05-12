namespace Vetly.Domain.Entities;

public class Animal
{
    public Guid Id { get; private set; } // Guid para garantir que só exista um ID único para cada animal
    public string Nome { get; private set; }
    public string Especie { get; private set; }
    public string Raca { get; private set; }
    public DateTime DataNascimento { get; private set; }
    public Guid TutorId { get; private set; } // Guid para garantir que só exista um ID único para cada tutor
    public List<string> AlertasAtivos { get; private set; }
    public bool Ativo { get; private set; }

    private Animal()
    {
        Nome = null!; // Não será null quando for usado
        Especie = null!;
        Raca = null!;
        AlertasAtivos = []; // Inicializa a lista de alertas como vazia para evitar null reference exceptions
    }

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

    public void AtualizarDados(string nome, string especie, string raca, DateTime dataNascimento)
    {
        Nome = nome;
        Especie = especie;
        Raca = raca;
        DataNascimento = dataNascimento;
    }

    public void AdicionarAlerta(string alerta)
    {
        if (!AlertasAtivos.Contains(alerta))
            AlertasAtivos.Add(alerta);
    }

    public void RemoverAlerta(string alerta) => AlertasAtivos.Remove(alerta);

    public void Desativar() => Ativo = false;

    public int IdadeEmAnos() => (int)((DateTime.UtcNow - DataNascimento).TotalDays / 365.25); //DateTime.UtcNow é um método que retorna a data e hora atual em formato UTC
}
