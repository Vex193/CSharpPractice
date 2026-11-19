using System;

namespace BookingSystem;

/// <summary>
/// Статус бронирования после оплаты
/// </summary>
public enum BookingStatus
{
    /// <summary>
    /// Платёж принят
    /// </summary>
    Confirmed,

    /// <summary>
    /// Платёж отклонён
    /// </summary>
    PaymentFailed
}

/// <summary>
/// Данные для бронирования мест
/// </summary>
public sealed record BookingRequest(
    int EventId,
    string CustomerEmail,
    int SeatsCount,
    decimal PricePerSeat);

/// <summary>
/// Сохранённый результат бронирования
/// </summary>
public sealed record Booking(
    Guid Id,
    int EventId,
    string CustomerEmail,
    int SeatsCount,
    decimal TotalAmount,
    DateTimeOffset CreatedAt,
    BookingStatus Status);

/// <summary>
/// Доступ к мероприятиям и их местам
/// </summary>
public interface IEventRepository
{
    /// <summary>
    /// Проверяет существование мероприятия
    /// </summary>
    bool Exists(int eventId);

    /// <summary>
    /// Возвращает число свободных мест
    /// </summary>
    int GetAvailableSeats(int eventId);

    /// <summary>
    /// Резервирует места
    /// </summary>
    void ReserveSeats(int eventId, int seatsCount);

    /// <summary>
    /// Освобождает зарезервированные места
    /// </summary>
    void ReleaseSeats(int eventId, int seatsCount);
}

/// <summary>
/// Хранилище результатов бронирования
/// </summary>
public interface IBookingRepository
{
    /// <summary>
    /// Сохраняет результат бронирования
    /// </summary>
    void Save(Booking booking);
}

/// <summary>
/// Сервис оплаты бронирования
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Возвращает true, если платёж принят
    /// </summary>
    bool Charge(string customerEmail, decimal amount);
}

/// <summary>
/// Источник времени для бронирования
/// </summary>
public interface IClock
{
    /// <summary>
    /// Возвращает время в UTC
    /// </summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>
/// Генератор идентификаторов бронирования
/// </summary>
public interface IBookingIdGenerator
{
    /// <summary>
    /// Создаёт идентификатор бронирования
    /// </summary>
    Guid Create();
}

/// <summary>
/// Резервирует места и сохраняет результат оплаты
/// </summary>
public sealed class BookingService
{
    /// <summary>
    /// Репозиторий мероприятий
    /// </summary>
    private readonly IEventRepository _eventRepository;

    /// <summary>
    /// Репозиторий бронирований
    /// </summary>
    private readonly IBookingRepository _bookingRepository;

    /// <summary>
    /// Сервис оплаты
    /// </summary>
    private readonly IPaymentService _paymentService;

    /// <summary>
    /// Источник времени
    /// </summary>
    private readonly IClock _clock;

    /// <summary>
    /// Генератор идентификаторов
    /// </summary>
    private readonly IBookingIdGenerator _idGenerator;

    /// <summary>
    /// Подключает зависимости сервиса
    /// </summary>
    public BookingService(
        IEventRepository eventRepository,
        IBookingRepository bookingRepository,
        IPaymentService paymentService,
        IClock clock,
        IBookingIdGenerator idGenerator)
    {
        ArgumentNullException.ThrowIfNull(eventRepository);
        ArgumentNullException.ThrowIfNull(bookingRepository);
        ArgumentNullException.ThrowIfNull(paymentService);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(idGenerator);

        _eventRepository = eventRepository;
        _bookingRepository = bookingRepository;
        _paymentService = paymentService;
        _clock = clock;
        _idGenerator = idGenerator;
    }

    /// <summary>
    /// Создаёт бронирование для указанного мероприятия
    /// </summary>
    public Booking CreateBooking(BookingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            request.EventId, nameof(request.EventId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            request.PricePerSeat, nameof(request.PricePerSeat));

        if (string.IsNullOrWhiteSpace(request.CustomerEmail)
            || !request.CustomerEmail.Contains('@'))
        {
            throw new ArgumentException(
                "Адрес электронной почты должен содержать @",
                nameof(request.CustomerEmail));
        }

        if (request.SeatsCount is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.SeatsCount),
                "Допустимо 1–10 мест");
        }

        int eventId = request.EventId;
        int seatsCount = request.SeatsCount;
        if (!_eventRepository.Exists(eventId))
        {
            throw new InvalidOperationException(
                "Мероприятие не существует");
        }

        if (_eventRepository.GetAvailableSeats(eventId)
            < seatsCount)
        {
            throw new InvalidOperationException(
                "Недостаточно мест");
        }

        decimal totalAmount = seatsCount * request.PricePerSeat;
        Guid id = _idGenerator.Create();
        DateTimeOffset createdAt = _clock.UtcNow;
        _eventRepository.ReserveSeats(eventId, seatsCount);

        bool paid;
        try
        {
            paid = _paymentService.Charge(
                request.CustomerEmail, totalAmount);
        }
        catch
        {
            try
            {
                _eventRepository.ReleaseSeats(
                    eventId, seatsCount);
            }
            catch
            {
                // Сбой освобождения не заменяет ошибку оплаты
            }

            throw;
        }

        if (!paid)
        {
            _eventRepository.ReleaseSeats(eventId, seatsCount);
        }

        BookingStatus status = paid
            ? BookingStatus.Confirmed
            : BookingStatus.PaymentFailed;
        var booking = new Booking(
            id, eventId, request.CustomerEmail, seatsCount,
            totalAmount, createdAt, status);
        _bookingRepository.Save(booking);
        return booking;
    }
}
