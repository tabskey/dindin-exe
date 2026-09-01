using System.Globalization;
using System.Text;

namespace Domain.Entities;

/// <summary>
/// Monta o label de contraparte exibido no extrato: "{NOME} {NUMERO} CC" (ex.: "JOAO 00456-78 CC").
/// O número da conta é único por construção (índice único + retry na criação), então o label
/// identifica a contraparte sem ambiguidade — o antigo fragmento de CPF (NNN-NN) podia se repetir
/// entre contas diferentes (ex.: 233.333.333-33 e 333.333.333-33 → ambos "333-33").
/// Contraparte é sempre outra conta do sistema (resolvida por CPF ou número da conta) ou a própria
/// conta (auto-depósito "AUTO-DEPOSITO" ou auto-saque "AUTO-SAQUE" — o próprio titular).
/// </summary>
public static class CounterpartyLabel
{
    private const string Suffix = "CC";

    public static string For(Account account) =>
        $"{NormalizeName(account.Name)} {account.AccountNumber} {Suffix}";

    public static string AutoDeposit(Account account) =>
        $"AUTO-DEPOSITO {account.AccountNumber} {Suffix}";

    public static string AutoWithdrawal(Account account) =>
        $"AUTO-SAQUE {account.AccountNumber} {Suffix}";

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
