using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.Domain.Entities;

public class Veterinario
{
    public Guid Id { get; private set; } //Guid para garantir que só exista um ID único para cada veterinário
    public string Nome { get; private set; }
    public Crmv Crmv { get; private set; }
    public string UfAtuacao { get; private set; } //UfAtuacao é o estado onde o veterinário atua, e deve ser armazenado em maiúsculo para facilitar buscas e comparações
    public List<string> Especialidades { get; private set; }
    public List<string> EspeciesAtendidas { get; private set; }
    public string? TitulacaoAcademica { get; private set; }
    public PersonaVeterinario Persona { get; private set; }
    public PlanoAssinatura Plano { get; private set; }
    public bool Ativo { get; private set; }
    public Guid? EmpresaId { get; private set; } //Guid? para garantir que só exista um ID único para cada empresa, e nullable para permitir que o veterinário não esteja vinculado a nenhuma empresa

    private Veterinario()
    {
        Nome = null!;
        Crmv = null!;
        UfAtuacao = null!;
        Especialidades = [];
        EspeciesAtendidas = [];
    }

    public Veterinario(string nome, Crmv crmv, string ufAtuacao, PersonaVeterinario persona, PlanoAssinatura plano)
    {
        Id = Guid.NewGuid();
        Nome = nome;
        Crmv = crmv;
        UfAtuacao = ufAtuacao.ToUpperInvariant();
        Persona = persona;
        Plano = plano;
        Especialidades = [];
        EspeciesAtendidas = [];
        Ativo = true;
    }

    public void AtualizarDados(string nome, string ufAtuacao, string? titulacao)
    {
        Nome = nome;
        UfAtuacao = ufAtuacao.ToUpperInvariant();
        TitulacaoAcademica = titulacao;
    }

    public void AdicionarEspecialidade(string especialidade)
    {
        if (!Especialidades.Contains(especialidade))
            Especialidades.Add(especialidade);
    }

    public void AdicionarEspecie(string especie)
    {
        if (!EspeciesAtendidas.Contains(especie))
            EspeciesAtendidas.Add(especie);
    }

    public void VincularEmpresa(Guid empresaId)
    {
        EmpresaId = empresaId;
        Persona = PersonaVeterinario.Vinculado;
    }

    public void Desativar() => Ativo = false;

    public void AtualizarPlano(PlanoAssinatura plano) => Plano = plano;
}
