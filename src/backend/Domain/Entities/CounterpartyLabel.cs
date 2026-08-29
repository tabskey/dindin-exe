using System.Globalization;
using System.Text;

namespace Domain.Entities;

/// <summary>
/// Monta o label de contraparte exibido no extrato: "{NOME} {NNN-NN} CC" (ex.: "JOAO789-09 CC").
/// Contraparte é sempre outra conta do sistema (resolvida por CPF) ou a própria conta
/// (depósito na boca do caixa, rotulado "AUTO-DEPOSITO").
/// </summary>
public static class CounterpartyLabel
{
    private const string Suffix = "CC";

    public static string For(Account account) =>
        $"{NormalizeName(account.Name)} {MaskCpf(account.Cpf)} {Suffix}";

    public static string AutoDeposit(Account account) =>
        $"AUTO-DEPOSITO {MaskCpf(account.Cpf)} {Suffix}";

    /// <summary>Máscara do CPF: últimos 5 dígitos no formato NNN-NN (ex.: "123.456.789-09" → "789-09").</summary>
    public static string MaskCpf(string cpf)
    {
        var digits = new string(cpf.Where(char.IsAsciiDigit).ToArray());
        return digits.Length >= 5
            ? $"{digits[^5..^2]}-{digits[^2..]}"
            : digits;
    }

    private static string NormalizeName(string name)
    {
        var decomposed = name.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().ToUpperInvariant();
    }
}
