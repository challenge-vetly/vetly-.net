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

    /// <summary><c>GET /api/marketplace/itens</c>. Padrão: <c>nome</c> crescente.</summary>
    public static class Marketplace
    {
        /// <summary>Nome do item, em ordem alfabética.</summary>
        public const string Nome = "nome";

        /// <summary>Custo em pontos.</summary>
        public const string CustoEmPontos = "custoempontos";

        /// <summary>Momento do cadastro — "novidades" primeiro com <c>desc</c>.</summary>
        public const string CriadoEm = "criadoem";

        /// <summary>Todos os campos aceitos.</summary>
        public static readonly IReadOnlyCollection<string> Todos = [Nome, CustoEmPontos, CriadoEm];
    }

    /// <summary><c>GET /api/animais/{id}/avatar/historico</c>. Padrão: <c>data</c> decrescente.</summary>
    public static class HistoricoDoAvatar
    {
        /// <summary>Dia do registro.</summary>
        public const string Data = "data";

        /// <summary>Saúde do dia — "piores dias" primeiro com <c>asc</c>.</summary>
        public const string Saude = "saude";

        /// <summary>Humor do dia.</summary>
        public const string Humor = "humor";

        /// <summary>Todos os campos aceitos.</summary>
        public static readonly IReadOnlyCollection<string> Todos = [Data, Saude, Humor];
    }

    /// <summary><c>GET /api/tutores/{id}/carteira/historico</c>. Padrão: <c>momento</c> decrescente.</summary>
    public static class Carteira
    {
        /// <summary>Quando o lançamento aconteceu.</summary>
        public const string Momento = "momento";

        /// <summary>Valor em reais do lançamento.</summary>
        public const string Valor = "valor";

        /// <summary>Todos os campos aceitos.</summary>
        public static readonly IReadOnlyCollection<string> Todos = [Momento, Valor];
    }
}
