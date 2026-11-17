using System;
using System.Collections.Generic;
using System.Linq;

namespace Practice.Linq;

/// <summary>
/// Категория товаров
/// </summary>
public sealed record Category(int Id, string Name);

/// <summary>
/// Поставщик товаров
/// </summary>
public sealed record Supplier(
    int Id, string Name, decimal Rating);

/// <summary>
/// Товар и его текущий остаток
/// </summary>
public sealed record Product(
    int Id,
    string Name,
    int CategoryId,
    int? SupplierId,
    decimal Price,
    int StockQuantity,
    bool IsActive,
    DateTime CreatedAt);

/// <summary>
/// Позиция заказа с количеством и ценой на момент покупки
/// </summary>
public sealed record OrderItem(
    int ProductId,
    int Quantity,
    decimal UnitPrice);

/// <summary>
/// Заказ покупателя со списком позиций
/// </summary>
public sealed record Order(
    int Id,
    int CustomerId,
    DateTime CreatedAt,
    IReadOnlyList<OrderItem> Items);

/// <summary>
/// Данные товара для отображения в каталоге
/// </summary>
public sealed record ProductCatalogItem(
    int Id,
    string Name,
    string CategoryName,
    string SupplierName,
    decimal Price,
    decimal PriceWithTax,
    string StockStatus,
    bool IsActive);

/// <summary>
/// Общие показатели, каталог и отчёты по остаткам и продажам
/// </summary>
public sealed record StoreAnalyticsReport(
    int TotalProducts,
    int ActiveProducts,
    int OutOfStockProducts,
    decimal TotalStockValue,
    decimal? AverageActiveProductPrice,
    IReadOnlyList<ProductCatalogItem> CatalogPage,
    IReadOnlyList<CategoryStockReport> Categories,
    IReadOnlyList<ProductSalesReport> TopSellingProducts);

/// <summary>
/// Сводные показатели остатков по категории
/// </summary>
public sealed record CategoryStockReport(
    string CategoryName,
    int ProductCount,
    int ActiveProductCount,
    int InStockProductCount,
    int TotalStockQuantity,
    decimal TotalStockValue,
    decimal? AveragePrice,
    decimal? MinPrice,
    decimal? MaxPrice);

/// <summary>
/// Сводные показатели продаж товара
/// </summary>
public sealed record ProductSalesReport(
    int ProductId,
    string ProductName,
    int TotalQuantity,
    int OrdersCount,
    decimal Revenue);

/// <summary>
/// Аналитика коллекций в памяти с проверкой данных
/// </summary>
public static class StoreAnalyticsService
{
    /// <summary>
    /// Формирует отчёт без изменения исходных коллекций
    /// </summary>
    public static StoreAnalyticsReport BuildReport(
        IEnumerable<Category> categories,
        IEnumerable<Supplier> suppliers,
        IEnumerable<Product> products,
        IEnumerable<Order> orders,
        decimal? minPrice,
        decimal? maxPrice,
        int? categoryId,
        string? searchText,
        int pageNumber,
        int pageSize)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(suppliers);
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        if (minPrice > maxPrice)
        {
            throw new ArgumentException(
                "Minimum price must not exceed maximum price",
                nameof(minPrice));
        }

        // Сохраняем источники один раз для проверки и отчётов
        // Поддерживаем однократно читаемые последовательности
        Category[] categorySnapshot = Snapshot(
            categories, category => category.Id,
            nameof(categories));
        Supplier[] supplierSnapshot = Snapshot(
            suppliers, supplier => supplier.Id,
            nameof(suppliers));
        Product[] productSnapshot = Snapshot(
            products, product => product.Id, nameof(products));
        Order[] orderSnapshot = Snapshot(
            orders, order => order.Id, nameof(orders));

        if (categorySnapshot.Any(
            category => category.Name is null)
            || supplierSnapshot.Any(
                supplier => supplier.Name is null))
        {
            throw new ArgumentException(
                "Category and supplier names must not be null");
        }

        if (productSnapshot.Any(product => product.Name is null
            || product.StockQuantity < 0))
        {
            throw new ArgumentException(
                "Products need a name and nonnegative stock",
                nameof(products));
        }

        bool allPricesValid = productSnapshot.All(
            product => product.Price >= 0);
        if (!allPricesValid)
        {
            throw new ArgumentException(
                "Product prices must be nonnegative",
                nameof(products));
        }

        // All равен true и для пустой последовательности

        for (var index = 0;
            index < orderSnapshot.Length; index++)
        {
            Order order = orderSnapshot[index];
            if (order.Items is null)
            {
                throw new ArgumentException(
                    $"Order {order.Id} has null Items",
                    nameof(orders));
            }

            // Items может ссылаться на изменяемый список
            // Копируем позиции для согласованности отчётов
            OrderItem[] items = order.Items.ToArray();
            if (items.Any(item => item is null
                || item.Quantity <= 0 || item.UnitPrice < 0))
            {
                throw new ArgumentException(
                    $"Order {order.Id} "
                    + "has a null or invalid line: "
                    + "Quantity must be positive "
                    + "and UnitPrice nonnegative",
                    nameof(orders));
            }

            orderSnapshot[index] = order with { Items = items };
        }

        bool hasOutOfStock = productSnapshot.Any(
            product => product.StockQuantity == 0);
        Product? expensiveProduct =
            productSnapshot.FirstOrDefault(
                product => product.Price > 100_000m);
        Console.WriteLine(
            $"Any: есть нулевой остаток = {hasOutOfStock}");
        Console.WriteLine(
            $"All: цены неотрицательны = {allPricesValid}");
        Console.WriteLine(
            "FirstOrDefault > 100000: "
            + $"{expensiveProduct?.Name ?? "нет"}");
        if (categorySnapshot.Length > 0)
        {
            int existingId = categorySnapshot[0].Id;
            Category category = categorySnapshot.Single(
                candidate => candidate.Id == existingId);
            Console.WriteLine(
                $"Single по ID {existingId}: {category.Name}");
        }

        products = productSnapshot;
        IEnumerable<Product> filteredProducts = products;
        filteredProducts = filteredProducts.Where(
            product => product.IsActive);
        if (minPrice.HasValue)
        {
            filteredProducts = filteredProducts.Where(
                product => product.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            filteredProducts = filteredProducts.Where(
                product => product.Price <= maxPrice.Value);
        }

        if (categoryId.HasValue)
        {
            filteredProducts = filteredProducts.Where(
                product =>
                    product.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrEmpty(searchText))
        {
            filteredProducts = filteredProducts.Where(
                product => product.Name.Contains(
                    searchText,
                    StringComparison.OrdinalIgnoreCase));
        }

        filteredProducts = filteredProducts.Where(
            product => product.StockQuantity > 0);

        var catalogQuery =
            from product in filteredProducts
            join category in categorySnapshot
                on product.CategoryId equals category.Id
                into matchingCategories
            from category in matchingCategories.DefaultIfEmpty()
            join supplier in supplierSnapshot
                on product.SupplierId equals (int?)supplier.Id
                into matchingSuppliers
            from supplier in matchingSuppliers.DefaultIfEmpty()
            select new ProductCatalogItem(
                product.Id,
                product.Name,
                category?.Name ?? "Неизвестная категория",
                supplier?.Name ?? "Не указан",
                product.Price,
                decimal.Round(product.Price * 1.2m, 2,
                    MidpointRounding.AwayFromZero),
                product.StockQuantity == 0 ? "Нет в наличии"
                    : product.StockQuantity <= 5
                        ? "Мало осталось"
                    : "В наличии",
                product.IsActive);

        // OrderBy фиксирует порядок до разбиения на страницы
        // Id различает товары с одинаковыми ценой и названием
        // ToList до фильтров и пагинации хранит лишние строки
        // Сортируем тоже до материализации страницы
        // Снимки нужны для проверки и повторных отчётов
        var sortedCatalog = catalogQuery
            .OrderBy(item => item.Price)
            .ThenBy(item => item.Name)
            .ThenBy(item => item.Id);
        long offset = ((long)pageNumber - 1) * pageSize;
        List<ProductCatalogItem> catalogPage =
            offset > int.MaxValue
                ? new()
                : sortedCatalog.Skip((int)offset)
                    .Take(pageSize).ToList();

        // GroupJoin сохраняет категории без товаров
        List<CategoryStockReport> categoryReports =
            categorySnapshot
            .GroupJoin(productSnapshot,
                category => category.Id,
                product => product.CategoryId,
                (category, categoryProducts) =>
                new CategoryStockReport(
                    category.Name,
                    categoryProducts.Count(),
                    categoryProducts.Count(
                        product => product.IsActive),
                    categoryProducts.Count(
                        product => product.StockQuantity > 0),
                    categoryProducts.Sum(
                        product => product.StockQuantity),
                    categoryProducts.Sum(
                        product =>
                            product.Price
                            * product.StockQuantity),
                    categoryProducts
                        .Select(
                            product => (decimal?)product.Price)
                        .Average(),
                    categoryProducts
                        .Select(
                            product => (decimal?)product.Price)
                        .Min(),
                    categoryProducts
                        .Select(
                            product => (decimal?)product.Price)
                        .Max()))
            .OrderBy(category => category.CategoryName)
            .ToList();

        // SelectMany разворачивает позиции в общий поток
        // Select оставил бы вложенные последовательности
        var salesLines = orderSnapshot.SelectMany(order =>
            order.Items.Select(item => new
            {
                OrderId = order.Id,
                OrderDate = order.CreatedAt,
                ProductId = item.ProductId,
                item.Quantity,
                item.UnitPrice,
                LineTotal = item.Quantity * item.UnitPrice
            }));

        // Join исключает непроданные и неизвестные товары
        // Выручку считаем по UnitPrice на момент покупки
        // Distinct считает каждый заказ с товаром один раз
        List<ProductSalesReport> topSellingProducts = salesLines
            .Join(productSnapshot,
                line => line.ProductId,
                product => product.Id,
                (line, product) => new
                {
                    Line = line,
                    Product = product
                })
            .GroupBy(sale => new
            {
                sale.Product.Id,
                sale.Product.Name
            })
            .Select(group => new ProductSalesReport(
                group.Key.Id,
                group.Key.Name,
                group.Sum(sale => sale.Line.Quantity),
                group.Select(sale => sale.Line.OrderId)
                    .Distinct().Count(),
                group.Sum(sale => sale.Line.LineTotal)))
            .OrderByDescending(sale => sale.Revenue)
            .ThenByDescending(sale => sale.TotalQuantity)
            .ThenBy(sale => sale.ProductName)
            .ThenBy(sale => sale.ProductId)
            .Take(5)
            .ToList();

        // Общие показатели считаем без фильтров каталога
        return new StoreAnalyticsReport(
            productSnapshot.Length,
            productSnapshot.Count(product => product.IsActive),
            productSnapshot.Count(
                product => product.StockQuantity == 0),
            productSnapshot.Sum(
                product =>
                    product.Price * product.StockQuantity),
            productSnapshot.Where(product => product.IsActive)
                .Select(product => (decimal?)product.Price)
                .Average(),
            catalogPage,
            categoryReports,
            topSellingProducts);
    }

    /// <summary>
    /// Копирует и проверяет последовательности перед соединением
    /// </summary>
    private static T[] Snapshot<T>(
        IEnumerable<T> source,
        Func<T, int> getId,
        string parameterName) where T : class
    {
        T[] snapshot = source.ToArray();
        if (snapshot.Any(item => item is null))
        {
            throw new ArgumentException(
                "Sequence must not contain null elements",
                parameterName);
        }

        if (snapshot.Select(getId).Distinct().Count()
            != snapshot.Length)
        {
            throw new ArgumentException(
                "Entity IDs must be unique", parameterName);
        }

        return snapshot;
    }
}

/// <summary>
/// Фиксированные тестовые данные с граничными случаями
/// </summary>
public static class TestDataFactory
{
    /// <summary>
    /// Создаёт независимые списки для каждой демонстрации
    /// </summary>
    public static (
        List<Category> Categories,
        List<Supplier> Suppliers,
        List<Product> Products,
        List<Order> Orders) Create()
    {
        var categories = new List<Category>
        {
            new(1, "Электроника"),
            new(2, "Книги"),
            new(3, "Дом"),
            new(4, "Пустая категория")
        };
        List<Supplier> suppliers = Enumerable.Range(1, 5)
            .Select(id => new Supplier(
                id, $"Поставщик {id}", 4m))
            .ToList();
        var createdAt = new DateTime(2026, 1, 15);
        List<Product> products = Enumerable.Range(1, 20)
            .Select(id => new Product(
                id,
                $"Товар {id:00}",
                (id - 1) % 3 + 1,
                id == 1 ? null : (id - 1) % 4 + 1,
                (id % 4 + 1) * 500m,
                id == 2 ? 0 : id % 10 + 1,
                id != 3,
                createdAt.AddDays(id)))
            .ToList();

        List<Order> orders = Enumerable.Range(1, 8)
            .Select(id => new Order(
                id,
                (id - 1) % 3 + 1,
                createdAt.AddDays(id + 30),
                new List<OrderItem>
                {
                    new(1, 1, 900m),
                    new(id + 1, 2, 400m + id * 100m),
                    new(id + 9, 1, 600m)
                }))
            .ToList();

        // В заказе 1 две позиции товара 1, но это один заказ
        orders[0] = orders[0] with
        {
            Items = orders[0].Items
                .Append(new OrderItem(1, 2, 850m)).ToArray()
        };
        return (categories, suppliers, products, orders);
    }
}

/// <summary>
/// Консольный отчёт и демонстрация выполнения LINQ
/// </summary>
public static class Program
{
    /// <summary>
    /// Выводит полный отчёт и примеры работы LINQ
    /// </summary>
    public static void Main()
    {
        (
            List<Category> Categories,
            List<Supplier> Suppliers,
            List<Product> Products,
            List<Order> Orders
        ) data = TestDataFactory.Create();
        StoreAnalyticsReport report =
            StoreAnalyticsService.BuildReport(
                data.Categories, data.Suppliers,
                data.Products, data.Orders,
                null, null, null, null, 1, 100);

        Console.WriteLine("АНАЛИТИКА МАГАЗИНА");
        Console.WriteLine(
            $"Всего товаров: {report.TotalProducts}");
        Console.WriteLine($"Активных: {report.ActiveProducts}");
        Console.WriteLine(
            $"Без остатка: {report.OutOfStockProducts}");
        Console.WriteLine(
            $"Стоимость запасов: {report.TotalStockValue:F2}");
        Console.WriteLine(
            $"Средняя активная цена: "
            + $"{report.AverageActiveProductPrice:F2}");
        Console.WriteLine(
            "КАТАЛОГ: первая страница, размер 100");
        foreach (ProductCatalogItem item in report.CatalogPage)
        {
            Console.WriteLine(
                $"{item.Id}: {item.Name} | "
                + $"{item.CategoryName} | "
                + $"{item.SupplierName} | "
                + $"цена {item.Price:F2} | "
                + $"с НДС {item.PriceWithTax:F2} | "
                + $"{item.StockStatus} | "
                + $"активен {item.IsActive}");
        }

        Console.WriteLine("ОСТАТКИ ПО КАТЕГОРИЯМ");
        foreach (CategoryStockReport category
            in report.Categories)
        {
            Console.WriteLine(
                $"{category.CategoryName}: "
                + $"товаров {category.ProductCount}, "
                + $"активных {category.ActiveProductCount}, "
                + $"в наличии {category.InStockProductCount}, "
                + $"остаток {category.TotalStockQuantity}, "
                + $"стоимость {category.TotalStockValue:F2}, "
                + "средняя "
                + $"{FormatPrice(category.AveragePrice)}, "
                + $"мин {FormatPrice(category.MinPrice)}, "
                + $"макс {FormatPrice(category.MaxPrice)}");
        }

        Console.WriteLine("ТОП-5 ПРОДАЖ");
        foreach (ProductSalesReport sale
            in report.TopSellingProducts)
        {
            Console.WriteLine(
                $"{sale.ProductId}: {sale.ProductName} | "
                + $"единиц {sale.TotalQuantity} | "
                + $"заказов {sale.OrdersCount} | "
                + $"выручка {sale.Revenue:F2}");
        }

        Console.WriteLine("КАТЕГОРИЯ И ЦЕНОВОЙ ДИАПАЗОН");
        var priceGroups = data.Products.GroupBy(product => new
        {
            product.CategoryId,
            LowerBound =
                decimal.Floor(product.Price / 500m) * 500m
        }).OrderBy(group => group.Key.CategoryId)
            .ThenBy(group => group.Key.LowerBound);
        foreach (var group in priceGroups)
        {
            Console.WriteLine(
                $"Категория {group.Key.CategoryId}: "
                + $"[{group.Key.LowerBound}; "
                + $"{group.Key.LowerBound + 500m}) | "
                + $"{group.Count()} товаров");
        }

        // AsQueryable строит дерево выражений для списка
        // SQL не создаётся, данные не переносятся в базу
        // Вызов не ускоряет обработку этого списка
        IQueryable<Product> queryableProducts =
            data.Products.AsQueryable();
        IQueryable<Product> activeQuery =
            queryableProducts.Where(product => product.IsActive);
        // После AsEnumerable работают методы Enumerable
        // Дальнейшая обработка выполняется в приложении
        // Вызов не материализует данные
        IEnumerable<string> localNames =
            activeQuery.AsEnumerable()
            .Select(product => product.Name.ToUpperInvariant());
        Console.WriteLine(
            "IQueryable/AsEnumerable, в памяти: "
            + string.Join(", ", localNames));

        // Используем отдельный список, не меняя исходные данные
        var mutableProducts = new List<Product>(data.Products);
        IEnumerable<Product> deferredQuery =
            mutableProducts.Where(product =>
                product.IsActive && product.StockQuantity > 0);
        List<Product> snapshot = deferredQuery.ToList();
        Console.WriteLine(
            $"До изменения: запрос {deferredQuery.Count()}, "
            + $"снимок {snapshot.Count}");
        mutableProducts.Add(new Product(
            21, "Добавлен после ToList", 1, null, 100m, 1, true,
            new DateTime(2026, 3, 1)));
        Console.WriteLine(
            $"После изменения: запрос {deferredQuery.Count()}, "
            + $"снимок {snapshot.Count}");
        // Отложенный запрос видит добавленный товар
        // Состав снимка ToList не меняется
        // ToList не копирует вложенные изменяемые объекты
    }

    /// <summary>
    /// Форматирует цену, явно обозначая отсутствие значения
    /// </summary>
    private static string FormatPrice(decimal? price)
    {
        return price?.ToString("F2") ?? "null";
    }
}