using Vetly.Application.DTOs.Veterinario;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Porta de saída para a validação do CRMV junto ao conselho regional (RN-107).
///
/// O value object <c>Crmv</c> continua responsável pelo <b>formato</b> do registro e roda
/// antes; este adaptador é a validação <b>junto ao conselho</b>, que o formato não prova.
///
/// A implementação real troca apenas o registro no DI — nenhum serviço muda (§5.2).
/// </summary>
public interface ICrmvAdapter
{
    /// <summary>
    /// Consulta o conselho regional sobre um registro.
    /// Nunca lança por indisponibilidade: devolve <c>Indisponivel</c>, que mantém o
    /// perfil pendente de validação e fora do matching (RN-107).
    /// </summary>
    Task<ResultadoCrmvDto> ValidarRegistroAsync(string crmv, string uf);
}
