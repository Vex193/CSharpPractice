using System;

namespace Practice.DataTypes;

/// <summary>
/// Выводит контур ромба из символов X
/// </summary>
public static class DiamondPrinter
{
    /// <summary>
    /// Печатает ромб с положительными нечётными диагоналями
    /// </summary>
    public static void PrintDiamond(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        if (size % 2 == 0)
        {
            throw new ArgumentException(
                "Размер должен быть нечётным", nameof(size));
        }

        if (size == 1)
        {
            Console.WriteLine(" ");
            return;
        }

        int middle = size / 2;

        for (var row = 0; row < size; row++)
        {
            int padding = Math.Abs(middle - row);
            int width = size - 2 * padding;

            Console.Write(new string(' ', padding));
            Console.Write('X');

            if (width > 1)
            {
                Console.Write(new string(' ', width - 2));
                Console.Write('X');
            }

            Console.WriteLine();
        }
    }
}
