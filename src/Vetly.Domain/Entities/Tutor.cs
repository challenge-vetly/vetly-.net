namespace Vetly.Domain.Entities;

public class Tutor
{
    public Guid Id { get; private set; }
    public string Nome { get; private set; }
    public string Email { get; private set; }
    public string Telefone { get; private set; }
    public bool ConsentimentoAtendimento { get; private set; }
    public bool ConsentimentoLembretes { get; private set; }
    public bool ConsentimentoCompartilhamento { get; private set; }
    public DateTime? DataConsentimento { get; private set; }
    public bool Ativo { get; private set; }

    private Tutor() { }

    public Tutor(string nome, string email, string telefone)
    {
        Id = Guid.NewGuid();
        Nome = nome;
        Email = email;
        Telefone = telefone;
        Ativo = true;
    }

    public void AtualizarDados(string nome, string email, string telefone)
    {
        Nome = nome;
        Email = email;
        Telefone = telefone;
    }

    public void RegistrarConsentimento(bool atendimento, bool lembretes, bool compartilhamento)
    {
        ConsentimentoAtendimento = atendimento;
        ConsentimentoLembretes = lembretes;
        ConsentimentoCompartilhamento = compartilhamento;
        DataConsentimento = DateTime.UtcNow;
    }

    public void Desativar() => Ativo = false;
}
