using System;
using System.Globalization;
using System.Text;

namespace Practice.DataTypes;

/// <summary>
/// Рассчитывает накопления со сложными процентами
/// </summary>
public static class InterestCalculator
{
    /// <summary>
    /// Возвращает суммы по годам с двумя дробными знаками
    /// </summary>
    public static string CalculateCompoundInterest(
        decimal initialDeposit, int years, decimal interestRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            initialDeposit);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(years);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            interestRate);

        var report = new StringBuilder();
        decimal balance = initialDeposit;
        decimal annualRate = interestRate / 100m;

        for (var year = 0; year < years; year++)
        {
            balance += balance * annualRate;

            if (year > 0)
            {
                report.AppendLine();
            }

            string amount = balance.ToString(
                "F2", CultureInfo.InvariantCulture);
            report.Append($"Год {year + 1}: {amount} руб.");
        }

        return report.ToString();
    }
}
