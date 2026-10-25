namespace Vetly.Application.DTOs.Comum;

/// <summary>
/// Whitelist de ordenação de cada listagem (ver <see cref="Ordenacao"/>).
///
/// Fica num lugar só para que serviço, repositório e documentação falem dos mesmos
/// nomes: o serviço resolve o pedido contra a lista, o repositório traduz o campo
/// num <c>switch</c> fechado, e um campo novo exige mexer nos dois — de propósito.
/// </summary>
public static class CamposDeOrdenacao
{
    /// <summary><c>GET /api/consultas</c>. Padrão: <c>dataHora</c> decrescente.</summary>
    public static class Consultas
    {
        /// <summary>Data e hora do atendimento.</summary>
        public const string DataHora = "datahora";

        /// <summary>Estado na máquina de estados (RN-035/RN-038).</summary>
        public const string Status = "status";

        /// <summary>Todos os campos aceitos.</summary>
        public static readonly IReadOnlyCollection<string> Todos = [DataHora, Status];
    }

    /// <summary><c>GET /api/pagamentos</c>. Padrão: <c>momento</c> decrescente.</summary>
    public static class Pagamentos
    {
        /// <summary>Momento da cobrança.</summary>
        public const string Momento = "momento";

        /// <summary>Valor bruto da cobrança.</summary>
        public const string Valor = "valor";

        /// <summary>Estado da cobrança.</summary>
        public const string Status = "status";

        /// <summary>Todos os campos aceitos.</summary>
        public static readonly IReadOnlyCollection<string> Todos = [Momento, Valor, Status];
    }
}
