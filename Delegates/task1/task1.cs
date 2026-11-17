using System;
using System.Collections.Generic;

namespace Practice.Delegates;

/// <summary>
/// Позиция заказа
/// </summary>
public sealed class OrderItem
{
    /// <summary>
    /// Создаёт позицию заказа
    /// </summary>
    public OrderItem(string name, decimal price, int quantity)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        Price = price;
        Quantity = quantity;
    }

    /// <summary>
    /// Название товара
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Цена единицы товара
    /// </summary>
    public decimal Price { get; }

    /// <summary>
    /// Количество единиц товара
    /// </summary>
    public int Quantity { get; }
}

/// <summary>
/// Результат успешной обработки заказа
/// </summary>
public sealed class OrderProcessedEventArgs : EventArgs
{
    /// <summary>
    /// Создаёт данные события
    /// </summary>
    public OrderProcessedEventArgs(
        int orderId,
        decimal total,
        int processedItemsCount,
        DateTimeOffset processedAt)
    {
        OrderId = orderId;
        Total = total;
        ProcessedItemsCount = processedItemsCount;
        ProcessedAt = processedAt;
    }

    /// <summary>
    /// Идентификатор заказа
    /// </summary>
    public int OrderId { get; }

    /// <summary>
    /// Итоговая стоимость заказа
    /// </summary>
    public decimal Total { get; }

    /// <summary>
    /// Число включённых в расчёт позиций, а не единиц товара
    /// </summary>
    public int ProcessedItemsCount { get; }

    /// <summary>
    /// Время завершения обработки
    /// </summary>
    public DateTimeOffset ProcessedAt { get; }
}

/// <summary>
/// Рассчитывает стоимость позиции по преобразованной цене
/// </summary>
public delegate decimal ItemTotalCalculator(
    OrderItem item,
    decimal transformedUnitPrice);

/// <summary>
/// Последовательно обрабатывает позиции заказа
/// </summary>
public sealed class OrderProcessor
{
    /// <summary>
    /// Уведомляет об успешном завершении обработки
    /// </summary>
    public event EventHandler<OrderProcessedEventArgs>?
        OrderProcessed;

    /// <summary>
    /// Проверяет позиции и возвращает итоговую стоимость
    /// </summary>
    public decimal Process(
        int orderId,
        IEnumerable<OrderItem> items,
        Predicate<OrderItem> shouldProcess,
        Func<decimal, decimal> transformPrice,
        ItemTotalCalculator calculateItemTotal,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(shouldProcess);
        ArgumentNullException.ThrowIfNull(transformPrice);
        ArgumentNullException.ThrowIfNull(calculateItemTotal);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            orderId);

        log?.Invoke($"Начало обработки заказа {orderId}");
        var total = 0m;
        var processedItemsCount = 0;

        foreach (OrderItem item in items)
        {
            if (item is null)
            {
                throw new ArgumentException(
                    "Позиция заказа не должна быть null",
                    nameof(items));
            }

            if (item.Price < 0 || item.Quantity <= 0)
            {
                throw new ArgumentException(
                    "Цена должна быть неотрицательной, "
                    + "количество — положительным",
                    nameof(items));
            }

            if (!shouldProcess(item))
            {
                log?.Invoke($"Исключён товар: {item.Name}");
                continue;
            }

            decimal transformedPrice =
                transformPrice(item.Price);
            log?.Invoke(
                $"Цена {item.Name}: {item.Price:F2} → "
                + $"{transformedPrice:F2}");
            decimal itemTotal = calculateItemTotal(
                item, transformedPrice);
            total += itemTotal;
            processedItemsCount =
                checked(processedItemsCount + 1);
            log?.Invoke(
                $"Стоимость {item.Name}: {itemTotal:F2}");
        }

        log?.Invoke($"Заказ {orderId} завершён: {total:F2}");
        var result = new OrderProcessedEventArgs(
            orderId, total, processedItemsCount,
            DateTimeOffset.UtcNow);
        OnOrderProcessed(result);
        return total;
    }

    /// <summary>
    /// Вызывает подписчиков в порядке регистрации
    /// </summary>
    private void OnOrderProcessed(OrderProcessedEventArgs e)
    {
        // Ошибка подписчика прерывает цепочку и выходит наружу
        OrderProcessed?.Invoke(this, e);
    }
}

/// <summary>
/// Демонстрирует делегаты, замыкания и события
/// </summary>
public static class Program
{
    /// <summary>
    /// Обрабатывает заказы и демонстрирует ошибки подписчиков
    /// </summary>
    public static void Main()
    {
        var processor = new OrderProcessor();
        OrderItem[] items =
        {
            new("Книга", 100m, 2),
            new("Подарок", 0m, 1),
            new("Ручка", 20m, 3)
        };
        var journal = new List<string>();

        // Мультикаст вызывает оба обработчика журнала по порядку
        Action<string> log = Console.WriteLine;
        log += message => journal.Add(message);
        Predicate<OrderItem> shouldProcess =
            static item => item.Name != "Подарок";
        var discount = 0.1m;

        // Замыкание захватывает переменную, а не её копию
        Func<decimal, decimal> transformPrice = price =>
            decimal.Round(price * (1m - discount), 2,
                MidpointRounding.AwayFromZero);
        ItemTotalCalculator calculateItemTotal =
            static (item, price) => item.Quantity * price;
        EventHandler<OrderProcessedEventArgs> notify =
            static (_, result) => Console.WriteLine(
                $"Событие заказа {result.OrderId}: "
                + $"{result.Total:F2}, "
                + $"позиций {result.ProcessedItemsCount}");
        processor.OrderProcessed += notify;
        decimal total = processor.Process(
            1, items, shouldProcess, transformPrice,
            calculateItemTotal, log);
        Console.WriteLine($"Итог: {total:F2}");
        Console.WriteLine($"Записей в журнале: {journal.Count}");

        // Сохранённый делегат позволяет отписать обработчик
        processor.OrderProcessed -= notify;
        discount = 0.2m;
        Console.WriteLine("После отписки и изменения скидки:");
        processor.Process(
            2, items, shouldProcess, transformPrice,
            calculateItemTotal, log);

        var laterHandlerCalled = false;
        EventHandler<OrderProcessedEventArgs> failingHandler =
            static (_, _) => throw new InvalidOperationException(
                "Ошибка подписчика");
        EventHandler<OrderProcessedEventArgs> laterHandler =
            (_, _) => laterHandlerCalled = true;
        processor.OrderProcessed += failingHandler;
        processor.OrderProcessed += laterHandler;

        // Сумма рассчитана, но ошибка уведомления выходит наружу
        // После ошибки Invoke не вызывает остальных подписчиков
        try
        {
            processor.Process(
                3, items, shouldProcess, transformPrice,
                calculateItemTotal, log);
        }
        catch (InvalidOperationException exception)
        {
            Console.WriteLine(exception.Message);
        }
        finally
        {
            processor.OrderProcessed -= failingHandler;
            processor.OrderProcessed -= laterHandler;
        }

        Console.WriteLine(
            $"Следующий подписчик вызван: {laterHandlerCalled}");
    }
}
