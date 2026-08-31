using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para <see cref="MovimentoDePontos"/> (TB_MOVIMENTO_PONTOS).
///
/// Tabela append-only: nada aqui é atualizado nem removido pela aplicação.
/// </summary>
public class MovimentoDePontosConfiguration : IEntityTypeConfiguration<MovimentoDePontos>
{
    public void Configure(EntityTypeBuilder<MovimentoDePontos> builder)
    {
        builder.ToTable("TB_MOVIMENTO_PONTOS");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(m => m.TutorId)
            .HasColumnType("CHAR(36)").HasColumnName("TUTOR_ID").IsRequired();

        builder.Property(m => m.Tipo)
            .HasConversion<int>().HasColumnName("TIPO").IsRequired();

        // Assinado: negativo em débito e expiração, para que o saldo seja a soma da coluna
        builder.Property(m => m.Pontos)
            .HasColumnType("NUMBER(10)").HasColumnName("PONTOS").IsRequired();

        // Pontos antes do multiplicador e o multiplicador aplicado (RN-048): sem
        // guardar os dois, nao ha como explicar ao Responsavel de onde saiu o numero
        builder.Property(m => m.PontosBrutos)
            .HasColumnType("NUMBER(10)").HasColumnName("PONTOS_BRUTOS").IsRequired();

        builder.Property(m => m.Multiplicador)
            .HasColumnType("NUMBER(4,2)").HasColumnName("MULTIPLICADOR").IsRequired();

        // Saldo do lote: e o mecanismo do consumo FIFO (RN-050)
        builder.Property(m => m.Restante)
            .HasColumnType("NUMBER(10)").HasColumnName("RESTANTE").IsRequired();

        builder.Property(m => m.ObrigacaoId)
            .HasColumnType("CHAR(36)").HasColumnName("OBRIGACAO_ID");

        builder.Property(m => m.CupomId)
            .HasColumnType("CHAR(36)").HasColumnName("CUPOM_ID");

        builder.Property(m => m.ConsultaId)
            .HasColumnType("CHAR(36)").HasColumnName("CONSULTA_ID");

        builder.Property(m => m.PagamentoId)
            .HasColumnType("CHAR(36)").HasColumnName("PAGAMENTO_ID");

        builder.Property(m => m.ValorEmReais)
            .HasColumnType("NUMBER(18,2)").HasColumnName("VALOR_EM_REAIS");

        builder.Property(m => m.ExpiraEm).HasColumnName("EXPIRA_EM");

        builder.Property(m => m.MovimentoOrigemId)
            .HasColumnType("CHAR(36)").HasColumnName("MOVIMENTO_ORIGEM_ID");

        builder.Property(m => m.Descricao)
            .HasColumnType("VARCHAR2(200)").HasColumnName("DESCRICAO");

        builder.Property(m => m.OcorridoEm).HasColumnName("OCORRIDO_EM").IsRequired();

        // O saldo é somado por Responsável; a rotina de expiração varre por vencimento
        builder.HasIndex(m => new { m.TutorId, m.OcorridoEm })
            .HasDatabaseName("IX_PONTOS_TUTOR_OCORRIDO");

        builder.HasIndex(m => m.ExpiraEm).HasDatabaseName("IX_PONTOS_EXPIRA_EM");

        builder.HasIndex(m => m.ConsultaId).HasDatabaseName("IX_PONTOS_CONSULTA");

        builder.HasIndex(m => m.ObrigacaoId).HasDatabaseName("IX_PONTOS_OBRIGACAO");
    }
}

/// <summary>Configuração EF Core para <see cref="CupomResgate"/> (TB_CUPOM_RESGATE).</summary>
public class CupomResgateConfiguration : IEntityTypeConfiguration<CupomResgate>
{
    public void Configure(EntityTypeBuilder<CupomResgate> builder)
    {
        builder.ToTable("TB_CUPOM_RESGATE");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(c => c.TutorId)
            .HasColumnType("CHAR(36)").HasColumnName("TUTOR_ID").IsRequired();

        builder.Property(c => c.CodigoQr)
            .HasColumnType("VARCHAR2(40)").HasColumnName("CODIGO_QR").IsRequired();

        // O item viaja como texto porque o marketplace e mockado no MVP (RN-098); a
        // taxonomia da RN-099 fica preservada para quando a tabela real existir
        builder.Property(c => c.ItemRef)
            .HasColumnType("VARCHAR2(120)").HasColumnName("ITEM_REF").IsRequired();

        builder.Property(c => c.ItemNome)
            .HasColumnType("VARCHAR2(200)").HasColumnName("ITEM_NOME");

        builder.Property(c => c.Categoria)
            .HasConversion<int>().HasColumnName("ITEM_CATEGORIA").IsRequired();

        builder.Property(c => c.PontosDebitados)
            .HasColumnType("NUMBER(10)").HasColumnName("PONTOS_DEBITADOS").IsRequired();

        builder.Property(c => c.Desconto)
            .HasColumnType("NUMBER(18,2)").HasColumnName("DESCONTO").IsRequired();

        builder.Property(c => c.Faixa)
            .HasConversion<int>().HasColumnName("FAIXA").IsRequired();

        builder.Property(c => c.DescontoVetly)
            .HasColumnType("NUMBER(18,2)").HasColumnName("DESCONTO_VETLY").IsRequired();

        builder.Property(c => c.DescontoPrestador)
            .HasColumnType("NUMBER(18,2)").HasColumnName("DESCONTO_PRESTADOR").IsRequired();

        builder.Property(c => c.Status)
            .HasConversion<int>().HasColumnName("STATUS").IsRequired();

        builder.Property(c => c.EmitidoEm).HasColumnName("EMITIDO_EM").IsRequired();
        builder.Property(c => c.ExpiraEm).HasColumnName("EXPIRA_EM").IsRequired();
        builder.Property(c => c.ResgatadoEm).HasColumnName("RESGATADO_EM");

        // O codigo e apresentado no balcao: dois cupons com o mesmo codigo tornariam a
        // validacao ambigua
        builder.HasIndex(c => c.CodigoQr).HasDatabaseName("IX_CUPOM_CODIGO").IsUnique();

        builder.HasIndex(c => new { c.TutorId, c.EmitidoEm }).HasDatabaseName("IX_CUPOM_TUTOR");

        // A rotina de expiracao varre por status e vencimento
        builder.HasIndex(c => new { c.Status, c.ExpiraEm }).HasDatabaseName("IX_CUPOM_STATUS_EXPIRA");
    }
}
