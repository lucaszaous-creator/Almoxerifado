using ALMOXPRO.Application.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Globalization;

namespace ALMOXPRO.UI.ViewModels;

/// <summary>Linha editável da grade de itens do formulário de emissão de NF-e.</summary>
public partial class IssueNfeItemRow : ObservableObject
{
    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _ncm = string.Empty;

    /// <summary>5949 = outra saída não especificada (padrão para remessas diversas).</summary>
    [ObservableProperty]
    private string _cfop = "5949";

    [ObservableProperty]
    private string _unit = "UN";

    [ObservableProperty]
    private string _quantity = "1";

    [ObservableProperty]
    private string _unitValue = string.Empty;

    /// <summary>CST do ICMS na venda tributada (00, 20, 40, 41 ou 60).</summary>
    [ObservableProperty]
    private string _icmsCst = "00";

    /// <summary>Alíquota de ICMS % (CST 00 e 20).</summary>
    [ObservableProperty]
    private string _icmsRate = string.Empty;

    /// <summary>Redução da base de cálculo % (CST 20).</summary>
    [ObservableProperty]
    private string _icmsBaseReduction = string.Empty;

    public bool TryBuild(out IssueNfeItemInput item, out string error)
    {
        item = null!;
        if (!TryParseDecimal(Quantity, out var quantity))
        {
            error = $"Quantidade inválida no item \"{Description}\".";
            return false;
        }
        if (!TryParseDecimal(UnitValue, out var unitValue))
        {
            error = $"Valor unitário inválido no item \"{Description}\".";
            return false;
        }

        decimal? icmsRate = null;
        if (!string.IsNullOrWhiteSpace(IcmsRate))
        {
            if (!TryParseDecimal(IcmsRate, out var rate))
            {
                error = $"Alíquota de ICMS inválida no item \"{Description}\".";
                return false;
            }
            icmsRate = rate;
        }

        decimal? reduction = null;
        if (!string.IsNullOrWhiteSpace(IcmsBaseReduction))
        {
            if (!TryParseDecimal(IcmsBaseReduction, out var red))
            {
                error = $"Redução de base inválida no item \"{Description}\".";
                return false;
            }
            reduction = red;
        }

        item = new IssueNfeItemInput(Code.Trim(), Description.Trim(), Ncm.Trim(),
            Cfop.Trim(), Unit.Trim(), quantity, unitValue,
            string.IsNullOrWhiteSpace(IcmsCst) ? null : IcmsCst.Trim(), icmsRate, reduction);
        error = string.Empty;
        return true;
    }

    private static bool TryParseDecimal(string value, out decimal parsed) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed)
        || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed);
}
