using System.Globalization;
using System.Windows.Controls;

namespace ALMOXPRO.UI.Validation;

/// <summary>Validação inline de campo obrigatório (destaque em vermelho sob o campo).</summary>
public class RequiredRule : ValidationRule
{
    public string FieldName { get; set; } = "Este campo";

    public override ValidationResult Validate(object? value, CultureInfo cultureInfo) =>
        string.IsNullOrWhiteSpace(value as string)
            ? new ValidationResult(false, $"{FieldName} é obrigatório.")
            : ValidationResult.ValidResult;
}
