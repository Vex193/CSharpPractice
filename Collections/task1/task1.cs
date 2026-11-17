using System;
using System.Collections;
using System.Collections.Generic;

namespace Practice.Collections;

/// <summary>
/// Хранит элементы в массиве по принципу LIFO
/// </summary>
public class SmartStack<T> : IEnumerable<T>
{
    /// <summary>
    /// Задаёт начальную ёмкость стека
    /// </summary>
    private const int DefaultCapacity = 4;

    /// <summary>
    /// Хранит элементы от основания к вершине
    /// </summary>
    private T[] _items;

    /// <summary>
    /// Отслеживает изменения стека при перечислении
    /// </summary>
    private int _version;

    /// <summary>
    /// Возвращает количество элементов
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Возвращает длину внутреннего массива
    /// </summary>
    public int Capacity => _items.Length;

    /// <summary>
    /// Возвращает или заменяет элемент по глубине от вершины
    /// </summary>
    public T this[int depth]
    {
        get => _items[GetIndex(depth)];
        set
        {
            _items[GetIndex(depth)] = value;
            _version++;
        }
    }

    /// <summary>
    /// Создаёт пустой стек ёмкостью четыре элемента
    /// </summary>
    public SmartStack() : this(DefaultCapacity)
    {
    }

    /// <summary>
    /// Создаёт пустой стек указанной ёмкости
    /// </summary>
    public SmartStack(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            capacity, Array.MaxLength);

        _items = new T[capacity];
    }

    /// <summary>
    /// Копирует источник, помещая последний элемент на вершину
    /// </summary>
    public SmartStack(IEnumerable<T> collection) : this(0)
    {
        ArgumentNullException.ThrowIfNull(collection);

        foreach (T item in collection)
        {
            Push(item);
        }

        Array.Resize(ref _items, Count);
    }

    /// <summary>
    /// Добавляет элемент на вершину стека
    /// </summary>
    public void Push(T item)
    {
        if (Count == Array.MaxLength)
        {
            throw new OutOfMemoryException();
        }

        EnsureCapacity(Count + 1);
        _items[Count] = item;
        Count++;
        _version++;
    }

    /// <summary>
    /// Добавляет источник, делая последний элемент вершиной
    /// </summary>
    public void PushRange(IEnumerable<T> collection)
    {
        var pending = new SmartStack<T>(collection);

        if (pending.Count == 0)
        {
            return;
        }

        if (pending.Count > Array.MaxLength - Count)
        {
            throw new OutOfMemoryException();
        }

        EnsureCapacity(Count + pending.Count);
        Array.Copy(pending._items, 0,
            _items, Count, pending.Count);
        Count += pending.Count;
        _version++;
    }

    /// <summary>
    /// Удаляет и возвращает элемент с вершины
    /// </summary>
    public T Pop()
    {
        T item = Peek();
        Count--;
        _items[Count] = default!;
        _version++;
        return item;
    }

    /// <summary>
    /// Возвращает вершину без удаления элемента
    /// </summary>
    public T Peek()
    {
        if (Count == 0)
        {
            throw new InvalidOperationException("Стек пуст");
        }

        return _items[Count - 1];
    }

    /// <summary>
    /// Проверяет наличие элемента среди занятых ячеек
    /// </summary>
    public bool Contains(T item)
    {
        return Array.IndexOf(_items, item, 0, Count) >= 0;
    }

    /// <summary>
    /// Создаёт перечислитель от вершины к основанию
    /// </summary>
    public IEnumerator<T> GetEnumerator()
    {
        return Enumerate(_version).GetEnumerator();
    }

    /// <summary>
    /// Создаёт необобщённый перечислитель стека
    /// </summary>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Преобразует допустимую глубину в индекс массива
    /// </summary>
    private int GetIndex(int depth)
    {
        if (depth < 0 || depth >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(depth));
        }

        return Count - 1 - depth;
    }

    /// <summary>
    /// Перечисляет элементы неизменённого стека
    /// </summary>
    private IEnumerable<T> Enumerate(int version)
    {
        for (var index = Count - 1; index >= 0; index--)
        {
            CheckVersion(version);
            yield return _items[index];
        }

        CheckVersion(version);
    }

    /// <summary>
    /// Проверяет отсутствие изменений во время перечисления
    /// </summary>
    private void CheckVersion(int version)
    {
        if (version != _version)
        {
            throw new InvalidOperationException(
                "Стек изменён во время перечисления");
        }
    }

    /// <summary>
    /// Увеличивает ёмкость удвоениями до нужного размера
    /// </summary>
    private void EnsureCapacity(int requiredCapacity)
    {
        if (requiredCapacity <= Capacity)
        {
            return;
        }

        int capacity = Capacity == 0
            ? DefaultCapacity : Capacity;

        while (capacity < requiredCapacity)
        {
            capacity = (int)Math.Min(
                (long)capacity * 2, Array.MaxLength);
        }

        Array.Resize(ref _items, capacity);
    }
}

/// <summary>
/// Демонстрирует основные операции умного стека
/// </summary>
internal static class SmartStackDemo
{
    /// <summary>
    /// Создаёт стек, добавляет и удаляет элементы
    /// </summary>
    public static void Main()
    {
        var stack = new SmartStack<int>(new[] { 1, 2, 3 });
        stack.Push(4);
        stack.PushRange(new[] { 5, 6 });

        Console.Write("Стек от вершины: ");

        foreach (int item in stack)
        {
            Console.Write($"{item} ");
        }

        Console.WriteLine();
        Console.WriteLine($"Количество: {stack.Count}");
        Console.WriteLine($"Ёмкость: {stack.Capacity}");
        Console.WriteLine($"Вершина: {stack.Peek()}");
        Console.WriteLine($"Удалён: {stack.Pop()}");
        Console.WriteLine($"Содержит 3: {stack.Contains(3)}");
        Console.WriteLine($"После удаления: {stack.Count} " +
            $"элементов, ёмкость {stack.Capacity}");
        Console.WriteLine($"Элемент на глубине 1: {stack[1]}");
        stack[1] = 40;
        Console.WriteLine($"После замены: {stack[1]}");
    }
}
