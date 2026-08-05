using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Representa o responsável por um ou mais animais na plataforma Vetly.
/// Armazena os consentimentos LGPD de forma granular conforme exigência legal.
/// </summary>
public class Responsavel
{
    /// <summary>Identificador único do responsavel (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Nome completo do responsavel.</summary>
    [Required(ErrorMessage = "O nome é obrigatório.")]
    [MaxLength(200)]
    public string Nome { get; private set; }

    /// <summary>E-mail do responsavel. Usado para comunicações e lembretes, se consentido.</summary>
    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [MaxLength(254)] // RFC 5321: limite máximo de endereço de e-mail
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    public string Email { get; private set; }

    /// <summary>Telefone de contato do responsavel (com DDD).</summary>
    [Required(ErrorMessage = "O telefone é obrigatório.")]
    [MaxLength(20)]
    public string Telefone { get; private set; }

    /// <summary>Indica se o cadastro está ativo. Desativação é feita via soft delete.</summary>
    public bool Ativo { get; private set; }

    /// <summary>Tier de fidelidade atual (RN-071). Recalculado pela Fase 10 conforme os pontos válidos.</summary>
    public TierFidelidade TierFidelidade { get; private set; }

    /// <summary>Saldo de pontos de fidelidade ainda válidos (RN-070/074). Mutado pela Fase 10.</summary>
    public int SaldoPontos { get; private set; }

    /// <summary>
    /// Saldo de créditos Vetly acumulados (ex: crédito de cortesia por cancelamento do
    /// veterinário — RN-065). Mutado pela Fase 6.
    /// </summary>
    public decimal SaldoCreditosVetly { get; private set; }

    /// <summary>Quantidade de no-shows dentro da janela móvel de 90 dias (RN-064).</summary>
    public int ContadorNoShows { get; private set; }

    /// <summary>Data do no-show mais recente. Base para o cálculo da janela móvel de 90 dias.</summary>
    public DateTime? DataUltimoNoShow { get; private set; }

    /// <summary>Enquanto no futuro, indica que os descontos de fidelidade estão suspensos (RN-064).</summary>
    public DateTime? BloqueadoDescontosAte { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core para materialização de entidades.</summary>
    private Responsavel()
    {
        Nome = null!;
        Email = null!;
        Telefone = null!;
    }

    /// <summary>Cria um novo responsavel com os dados de contato obrigatórios.</summary>
    public Responsavel(string nome, string email, string telefone)
    {
        Id = Guid.NewGuid();
        Nome = nome;
        Email = email;
        Telefone = telefone;
        Ativo = true;
        TierFidelidade = TierFidelidade.Bronze;
    }

    /// <summary>Atualiza os dados de contato do responsavel.</summary>
    public void AtualizarDados(string nome, string email, string telefone)
    {
        Nome = nome;
        Email = email;
        Telefone = telefone;
    }

    /// <summary>Desativa o cadastro do responsavel (soft delete).</summary>
    public void Desativar() => Ativo = false;

    /// <summary>
    /// Registra um no-show ocorrido em <paramref name="agora"/>. A contagem usa uma
    /// janela móvel de 90 dias: se o último no-show já saiu dessa janela, o contador
    /// reinicia antes de somar o novo evento. Ao atingir 3 no-shows na janela,
    /// bloqueia descontos de fidelidade por 60 dias a partir de agora (RN-064).
    /// </summary>
    public void RegistrarNoShow(DateTime agora)
    {
        if (DataUltimoNoShow is null || (agora - DataUltimoNoShow.Value).TotalDays > 90)
            ContadorNoShows = 0;

        ContadorNoShows++;
        DataUltimoNoShow = agora;

        if (ContadorNoShows >= 3)
            BloqueadoDescontosAte = agora.AddDays(60);
    }

    /// <summary>
    /// Quantidade de no-shows ainda dentro da janela móvel de 90 dias, calculada em
    /// relação a <paramref name="agora"/>. Não muta estado — apenas projeta o valor
    /// que <see cref="ContadorNoShows"/> representaria se avaliado agora.
    /// </summary>
    public int NoShowsAtivos(DateTime agora)
    {
        if (DataUltimoNoShow is null || (agora - DataUltimoNoShow.Value).TotalDays > 90)
            return 0;

        return ContadorNoShows;
    }
}
