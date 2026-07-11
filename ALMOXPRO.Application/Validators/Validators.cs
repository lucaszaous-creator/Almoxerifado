using ALMOXPRO.Application.DTOs;
using FluentValidation;

namespace ALMOXPRO.Application.Validators;

public class UserUpsertValidator : AbstractValidator<UserUpsertDto>
{
    public UserUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("O nome é obrigatório.").MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().WithMessage("O e-mail é obrigatório.")
            .EmailAddress().WithMessage("E-mail inválido.").MaximumLength(150);
        RuleFor(x => x.Login).NotEmpty().WithMessage("O login é obrigatório.")
            .MinimumLength(3).WithMessage("O login deve ter ao menos 3 caracteres.").MaximumLength(50);
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("A senha é obrigatória para novos usuários.")
            .When(x => x.Id is null);
        RuleFor(x => x.Password)
            .MinimumLength(6).WithMessage("A senha deve ter ao menos 6 caracteres.")
            .When(x => !string.IsNullOrEmpty(x.Password));
    }
}

public class CategoryUpsertValidator : AbstractValidator<CategoryUpsertDto>
{
    public CategoryUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("O nome da categoria é obrigatório.").MaximumLength(100);
        RuleFor(x => x.ParentId)
            .Must((dto, parentId) => parentId != dto.Id)
            .WithMessage("A categoria não pode ser subcategoria de si mesma.")
            .When(x => x.Id.HasValue && x.ParentId.HasValue);
    }
}

public class SupplierUpsertValidator : AbstractValidator<SupplierUpsertDto>
{
    public SupplierUpsertValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().WithMessage("A razão social é obrigatória.").MaximumLength(200);
        RuleFor(x => x.Cnpj).NotEmpty().WithMessage("O CNPJ é obrigatório.")
            .Must(BeValidCnpj).WithMessage("CNPJ inválido.");
        RuleFor(x => x.Email).EmailAddress().WithMessage("E-mail inválido.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.State).Length(2).WithMessage("UF deve ter 2 letras.")
            .When(x => !string.IsNullOrWhiteSpace(x.State));
    }

    public static bool BeValidCnpj(string cnpj)
    {
        var digits = new string(cnpj.Where(char.IsDigit).ToArray());
        if (digits.Length != 14 || digits.Distinct().Count() == 1)
            return false;

        static int Digit(string input, int[] weights)
        {
            var sum = input.Take(weights.Length).Select((c, i) => (c - '0') * weights[i]).Sum();
            var mod = sum % 11;
            return mod < 2 ? 0 : 11 - mod;
        }

        int[] w1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] w2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        return digits[12] - '0' == Digit(digits, w1) && digits[13] - '0' == Digit(digits, w2);
    }
}

public class LocationUpsertValidator : AbstractValidator<LocationUpsertDto>
{
    public LocationUpsertValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0).WithMessage("Selecione o almoxarifado.");
        RuleFor(x => x.Building).NotEmpty().WithMessage("O prédio é obrigatório.").MaximumLength(50);
    }
}

public class ProductUpsertValidator : AbstractValidator<ProductUpsertDto>
{
    public ProductUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("O nome do produto é obrigatório.").MaximumLength(200);
        RuleFor(x => x.InternalCode).NotEmpty().WithMessage("O código interno é obrigatório.").MaximumLength(30);
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Selecione a categoria.");
        RuleFor(x => x.Unit).NotEmpty().WithMessage("A unidade é obrigatória.").MaximumLength(10);
        RuleFor(x => x.MinStock).GreaterThanOrEqualTo(0).WithMessage("O estoque mínimo não pode ser negativo.");
        RuleFor(x => x.MaxStock)
            .GreaterThanOrEqualTo(x => x.MinStock)
            .WithMessage("O estoque máximo deve ser maior ou igual ao mínimo.")
            .When(x => x.MaxStock > 0);
        RuleFor(x => x.TracksLot).Equal(true)
            .WithMessage("Produtos que controlam validade devem controlar lote.")
            .When(x => x.TracksExpiration);
    }
}

public class EntryCreateValidator : AbstractValidator<EntryCreateDto>
{
    public EntryCreateValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0).WithMessage("Selecione o almoxarifado.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Inclua ao menos um item na entrada.");
        RuleFor(x => x.SupplierId).NotNull()
            .WithMessage("Fornecedor é obrigatório para nota fiscal ou compra.")
            .When(x => x.Type is Domain.Common.EntryType.NotaFiscal or Domain.Common.EntryType.Compra);
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0).WithMessage("Item sem produto.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Quantidade deve ser maior que zero.");
            item.RuleFor(i => i.UnitCost).GreaterThanOrEqualTo(0).WithMessage("Valor unitário inválido.");
        });
    }
}

public class ExitCreateValidator : AbstractValidator<ExitCreateDto>
{
    public ExitCreateValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0).WithMessage("Selecione o almoxarifado.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Inclua ao menos um item na saída.");
        RuleFor(x => x.WorkOrder).NotEmpty()
            .WithMessage("Informe a ordem de serviço.")
            .When(x => x.Type == Domain.Common.ExitType.OrdemServico);
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0).WithMessage("Item sem produto.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Quantidade deve ser maior que zero.");
        });
    }
}

public class RequisitionCreateValidator : AbstractValidator<RequisitionCreateDto>
{
    public RequisitionCreateValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0).WithMessage("Selecione o almoxarifado.");
        RuleFor(x => x.SectorId).GreaterThan(0).WithMessage("Selecione o setor solicitante.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Inclua ao menos um item na requisição.");
        RuleFor(x => x.RequesterName).NotEmpty()
            .WithMessage("Informe o funcionário ou o nome de quem retira.")
            .When(x => x.EmployeeId is null);
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0).WithMessage("Item sem produto.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Quantidade deve ser maior que zero.");
        });
    }
}

public class TransferCreateValidator : AbstractValidator<TransferCreateDto>
{
    public TransferCreateValidator()
    {
        RuleFor(x => x.SourceWarehouseId).GreaterThan(0).WithMessage("Selecione o almoxarifado de origem.");
        RuleFor(x => x.DestinationWarehouseId).GreaterThan(0).WithMessage("Selecione o almoxarifado de destino.")
            .NotEqual(x => x.SourceWarehouseId).WithMessage("Origem e destino devem ser diferentes.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Inclua ao menos um item na transferência.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0).WithMessage("Item sem produto.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Quantidade deve ser maior que zero.");
        });
    }
}
