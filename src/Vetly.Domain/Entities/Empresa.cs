namespace Vetly.Domain.Entities;

public class Empresa
{
    public Guid Id { get; private set; }
    public string Nome { get; private set; }
    public string Tipo { get; private set; }
    public Guid AdministradorId { get; private set; }
    public bool Ativa { get; private set; }

    private Empresa()
    {
        Nome = null!;
        Tipo = null!;
    }

    public Empresa(string nome, string tipo, Guid administradorId)
    {
        Id = Guid.NewGuid();
        Nome = nome;
        Tipo = tipo;
        AdministradorId = administradorId;
        Ativa = true;
    }

    public void AtualizarDados(string nome, string tipo)
    {
        Nome = nome;
        Tipo = tipo;
    }

    public void AlterarAdministrador(Guid novoAdminId) => AdministradorId = novoAdminId;

    public void Desativar() => Ativa = false;
}
