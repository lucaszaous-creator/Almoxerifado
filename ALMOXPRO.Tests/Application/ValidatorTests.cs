using ALMOXPRO.Application.DTOs;
using ALMOXPRO.Application.Validators;
using ALMOXPRO.Domain.Common;
using Xunit;

namespace ALMOXPRO.Tests.Application;

public class SupplierValidatorTests
{
    private readonly SupplierUpsertValidator _validator = new();

    [Theory]
    [InlineData("11.222.333/0001-81")]
    [InlineData("11222333000181")]
    public void Cnpj_Valido_Passa(string cnpj)
    {
        Assert.True(SupplierUpsertValidator.BeValidCnpj(cnpj));
    }

    [Theory]
    [InlineData("11.222.333/0001-80")] // dígito verificador errado
    [InlineData("00000000000000")]     // todos iguais
    [InlineData("123")]                // curto demais
    [InlineData("")]
    public void Cnpj_Invalido_Falha(string cnpj)
    {
        Assert.False(SupplierUpsertValidator.BeValidCnpj(cnpj));
    }

    [Fact]
    public void Fornecedor_Valido_Passa()
    {
        var dto = new SupplierUpsertDto { CompanyName = "ACME LTDA", Cnpj = "11.222.333/0001-81" };

        var result = _validator.Validate(dto);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Fornecedor_SemRazaoSocial_Falha()
    {
        var dto = new SupplierUpsertDto { CompanyName = "", Cnpj = "11.222.333/0001-81" };

        Assert.False(_validator.Validate(dto).IsValid);
    }
}

public class ProductValidatorTests
{
    private readonly ProductUpsertValidator _validator = new();

    private static ProductUpsertDto ValidProduct() => new()
    {
        InternalCode = "P-001",
        Name = "Parafuso sextavado",
        CategoryId = 1,
        Unit = "UN",
        MinStock = 10,
        MaxStock = 100
    };

    [Fact]
    public void Produto_Valido_Passa()
    {
        Assert.True(_validator.Validate(ValidProduct()).IsValid);
    }

    [Fact]
    public void EstoqueMaximo_MenorQueMinimo_Falha()
    {
        var dto = ValidProduct();
        dto.MinStock = 50;
        dto.MaxStock = 10;

        Assert.False(_validator.Validate(dto).IsValid);
    }

    [Fact]
    public void ControlaValidade_SemControlarLote_Falha()
    {
        var dto = ValidProduct();
        dto.TracksExpiration = true;
        dto.TracksLot = false;

        Assert.False(_validator.Validate(dto).IsValid);
    }

    [Fact]
    public void ControlaValidade_ComLote_Passa()
    {
        var dto = ValidProduct();
        dto.TracksExpiration = true;
        dto.TracksLot = true;

        Assert.True(_validator.Validate(dto).IsValid);
    }
}

public class UserValidatorTests
{
    private readonly UserUpsertValidator _validator = new();

    [Fact]
    public void NovoUsuario_SemSenha_Falha()
    {
        var dto = new UserUpsertDto { Name = "Ana", Email = "ana@empresa.com", Login = "ana" };

        Assert.False(_validator.Validate(dto).IsValid);
    }

    [Fact]
    public void EdicaoUsuario_SemSenha_MantemAtual_Passa()
    {
        var dto = new UserUpsertDto { Id = 1, Name = "Ana", Email = "ana@empresa.com", Login = "ana" };

        Assert.True(_validator.Validate(dto).IsValid);
    }

    [Fact]
    public void SenhaCurta_Falha()
    {
        var dto = new UserUpsertDto { Name = "Ana", Email = "ana@empresa.com", Login = "ana", Password = "123" };

        Assert.False(_validator.Validate(dto).IsValid);
    }

    [Fact]
    public void EmailInvalido_Falha()
    {
        var dto = new UserUpsertDto { Name = "Ana", Email = "nao-eh-email", Login = "ana", Password = "123456" };

        Assert.False(_validator.Validate(dto).IsValid);
    }
}

public class CategoryValidatorTests
{
    private readonly CategoryUpsertValidator _validator = new();

    [Fact]
    public void Categoria_SubcategoriaDeSiMesma_Falha()
    {
        var dto = new CategoryUpsertDto { Id = 5, Name = "Ferramentas", ParentId = 5 };

        Assert.False(_validator.Validate(dto).IsValid);
    }

    [Fact]
    public void Categoria_ComPaiDiferente_Passa()
    {
        var dto = new CategoryUpsertDto { Id = 5, Name = "Ferramentas", ParentId = 2 };

        Assert.True(_validator.Validate(dto).IsValid);
    }
}

public class MovementValidatorTests
{
    [Fact]
    public void Entrada_NotaFiscal_SemFornecedor_Falha()
    {
        var dto = new EntryCreateDto
        {
            Type = EntryType.NotaFiscal,
            WarehouseId = 1,
            Items = [new EntryItemDto { ProductId = 1, Quantity = 1, UnitCost = 10 }]
        };

        Assert.False(new EntryCreateValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Entrada_Doacao_SemFornecedor_Passa()
    {
        var dto = new EntryCreateDto
        {
            Type = EntryType.Doacao,
            WarehouseId = 1,
            Items = [new EntryItemDto { ProductId = 1, Quantity = 1, UnitCost = 0 }]
        };

        Assert.True(new EntryCreateValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Entrada_SemItens_Falha()
    {
        var dto = new EntryCreateDto { Type = EntryType.Doacao, WarehouseId = 1 };

        Assert.False(new EntryCreateValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Entrada_ItemComQuantidadeZero_Falha()
    {
        var dto = new EntryCreateDto
        {
            Type = EntryType.Doacao,
            WarehouseId = 1,
            Items = [new EntryItemDto { ProductId = 1, Quantity = 0, UnitCost = 10 }]
        };

        Assert.False(new EntryCreateValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Saida_OrdemServico_SemNumeroOS_Falha()
    {
        var dto = new ExitCreateDto
        {
            Type = ExitType.OrdemServico,
            WarehouseId = 1,
            Items = [new ExitItemDto { ProductId = 1, Quantity = 1 }]
        };

        Assert.False(new ExitCreateValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Saida_Consumo_Valida_Passa()
    {
        var dto = new ExitCreateDto
        {
            Type = ExitType.Consumo,
            WarehouseId = 1,
            Items = [new ExitItemDto { ProductId = 1, Quantity = 2 }]
        };

        Assert.True(new ExitCreateValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Transferencia_OrigemIgualDestino_Falha()
    {
        var dto = new TransferCreateDto
        {
            SourceWarehouseId = 1,
            DestinationWarehouseId = 1,
            Items = [new TransferItemDto { ProductId = 1, Quantity = 1 }]
        };

        Assert.False(new TransferCreateValidator().Validate(dto).IsValid);
    }

    [Fact]
    public void Transferencia_Valida_Passa()
    {
        var dto = new TransferCreateDto
        {
            SourceWarehouseId = 1,
            DestinationWarehouseId = 2,
            Items = [new TransferItemDto { ProductId = 1, Quantity = 1 }]
        };

        Assert.True(new TransferCreateValidator().Validate(dto).IsValid);
    }
}
