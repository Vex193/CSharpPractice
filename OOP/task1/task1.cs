using System;
using System.Globalization;
using System.IO;

namespace Practice.Oop;

/// <summary>
/// Описывает товар и его основные характеристики
/// </summary>
public class Product
{
    /// <summary>
    /// Возвращает наименование товара
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Возвращает производителя товара
    /// </summary>
    public string Manufacturer { get; }

    /// <summary>
    /// Возвращает цену товара в рублях
    /// </summary>
    public decimal Price { get; }

    /// <summary>
    /// Возвращает срок годности в днях
    /// </summary>
    public int ShelfLifeDays { get; }

    /// <summary>
    /// Возвращает дату производства
    /// </summary>
    public DateOnly ProductionDate { get; }

    /// <summary>
    /// Создаёт товар с проверенными характеристиками
    /// </summary>
    public Product(string name, string manufacturer,
        decimal price, int shelfLifeDays,
        DateOnly productionDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(manufacturer);
        ArgumentOutOfRangeException.ThrowIfNegative(price);
        ArgumentOutOfRangeException.ThrowIfNegative(
            shelfLifeDays);

        Name = name.Trim();
        Manufacturer = manufacturer.Trim();
        Price = price;
        ShelfLifeDays = shelfLifeDays;
        ProductionDate = productionDate;
    }

    /// <summary>
    /// Возвращает текстовое представление товара
    /// </summary>
    public override string ToString()
    {
        string price = Price.ToString("F2",
            CultureInfo.GetCultureInfo("ru-RU"));
        string productionDate = ProductionDate.ToString(
            "dd.MM.yyyy", CultureInfo.InvariantCulture);

        return string.Join(Environment.NewLine,
            $"Наименование: {Name}",
            $"Производитель: {Manufacturer}",
            $"Цена: {price} руб.",
            $"Срок годности: {ShelfLifeDays} дн.",
            $"Дата производства: {productionDate}");
    }
}

/// <summary>
/// Демонстрирует создание и вывод товара через консоль
/// </summary>
internal static class ProductConsole
{
    /// <summary>
    /// Считывает характеристики и выводит описание товара
    /// </summary>
    public static int Main()
    {
        try
        {
            string name = ReadText("Наименование: ");
            string manufacturer = ReadText("Производитель: ");
            decimal price = ReadPrice();
            int shelfLifeDays = ReadShelfLifeDays();
            DateOnly productionDate = ReadProductionDate();

            var product = new Product(name, manufacturer,
                price, shelfLifeDays, productionDate);

            Console.WriteLine();
            Console.WriteLine(product);
            return 0;
        }
        catch (EndOfStreamException)
        {
            Console.Error.WriteLine(
                "Ввод завершён до заполнения всех данных");
            return 1;
        }
    }

    /// <summary>
    /// Считывает непустую строку без пробелов по краям
    /// </summary>
    private static string ReadText(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            string input = Console.ReadLine()
                ?? throw new EndOfStreamException();

            if (!string.IsNullOrWhiteSpace(input))
            {
                return input.Trim();
            }

            Console.WriteLine("Введите непустое значение");
        }
    }

    /// <summary>
    /// Считывает неотрицательную цену с точкой или запятой
    /// </summary>
    private static decimal ReadPrice()
    {
        while (true)
        {
            string input = ReadText("Цена в рублях: ")
                .Replace(',', '.');

            if (decimal.TryParse(input,
                NumberStyles.AllowDecimalPoint |
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out decimal price)
                && price >= 0)
            {
                return price;
            }

            Console.WriteLine("Введите неотрицательную цену");
        }
    }

    /// <summary>
    /// Считывает неотрицательный срок годности в днях
    /// </summary>
    private static int ReadShelfLifeDays()
    {
        while (true)
        {
            string input = ReadText("Срок годности в днях: ");

            if (int.TryParse(input, out int shelfLifeDays)
                && shelfLifeDays >= 0)
            {
                return shelfLifeDays;
            }

            Console.WriteLine(
                "Введите целое неотрицательное число");
        }
    }

    /// <summary>
    /// Считывает существующую дату производства
    /// </summary>
    private static DateOnly ReadProductionDate()
    {
        while (true)
        {
            string input = ReadText(
                "Дата производства (дд.ММ.гггг): ");

            if (DateOnly.TryParseExact(input, "dd.MM.yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly productionDate))
            {
                return productionDate;
            }

            Console.WriteLine(
                "Введите дату в формате дд.ММ.гггг");
        }
    }
}
