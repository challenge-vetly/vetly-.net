using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

public class Documento
{
    public Guid Id { get; private set; }
    public Guid? ConsultaId { get; private set; }
    public Guid? InternacaoId { get; private set; }
    public TipoDocumento TipoDocumento { get; private set; }
    public int Versao { get; private set; }
    public DateTime DataGeracao { get; private set; }
    public string CrmvSignatario { get; private set; }
    public bool AssinadoDigitalmente { get; private set; }

    private Documento()
    {
        CrmvSignatario = null!;
    }

    public Documento(TipoDocumento tipo, string crmvSignatario, Guid? consultaId = null, Guid? internacaoId = null)
    {
        Id = Guid.NewGuid();
        TipoDocumento = tipo;
        CrmvSignatario = crmvSignatario;
        ConsultaId = consultaId;
        InternacaoId = internacaoId;
        Versao = 1;
        DataGeracao = DateTime.UtcNow;
    }

    public void Assinar()
    {
        AssinadoDigitalmente = true;
    }

    public void IncrementarVersao() => Versao++;
}
