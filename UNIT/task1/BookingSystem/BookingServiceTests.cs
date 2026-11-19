using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DoNotParallelize]

namespace BookingSystem.Tests;

/// <summary>
/// Проверяет бронирование с подставными зависимостями
/// </summary>
[TestClass]
public sealed class BookingServiceTests
{
    /// <summary>
    /// Идентификатор для проверки результата
    /// </summary>
    private static readonly Guid s_bookingId =
        new("a45b62cc-23dc-4aad-a8a2-4bca3b298968");

    /// <summary>
    /// Фиксированное время для проверки результата
    /// </summary>
    private static readonly DateTimeOffset s_createdAt =
        new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Подставной репозиторий мероприятий
    /// </summary>
    private FakeEventRepository _events = null!;

    /// <summary>
    /// Подставной репозиторий бронирований
    /// </summary>
    private FakeBookingRepository _bookings = null!;

    /// <summary>
    /// Подставной платёжный сервис
    /// </summary>
    private FakePaymentService _payment = null!;

    /// <summary>
    /// Подставной источник времени
    /// </summary>
    private FakeClock _clock = null!;

    /// <summary>
    /// Подставной генератор идентификаторов
    /// </summary>
    private FakeBookingIdGenerator _idGenerator = null!;

    /// <summary>
    /// Проверяемый сервис
    /// </summary>
    private BookingService _service = null!;

    /// <summary>
    /// Создаёт независимые зависимости перед каждым тестом
    /// </summary>
    [TestInitialize]
    public void Initialize()
    {
        _events = new();
        _bookings = new();
        _payment = new();
        _clock = new(s_createdAt);
        _idGenerator = new(s_bookingId);
        _service = new(_events, _bookings, _payment, _clock,
            _idGenerator);
    }

    /// <summary>
    /// Проверяет успешное бронирование и действия сервиса
    /// </summary>
    [TestMethod]
    public void CreateBooking_PaymentSucceeds_ConfirmsBooking()
    {
        // Подготовка
        BookingRequest request = CreateRequest();
        var expected = new Booking(s_bookingId, request.EventId,
            request.CustomerEmail, 2, 250m, s_createdAt,
            BookingStatus.Confirmed);

        // Действие
        Booking booking = _service.CreateBooking(request);

        // Проверка
        Assert.AreEqual(expected, booking);
        Assert.AreEqual(1, _events.ReserveCalls);
        Assert.AreEqual(0, _events.ReleaseCalls);
        Assert.AreEqual(8, _events.AvailableSeats);
        Assert.AreEqual(1, _payment.ChargeCalls);
        Assert.AreEqual(250m, _payment.LastAmount);
        Assert.AreEqual(1, _bookings.SaveCalls);
        Assert.AreEqual(booking, _bookings.SavedBooking);
    }

    /// <summary>
    /// Проверяет возврат мест и сохранение отказа оплаты
    /// </summary>
    [TestMethod]
    public void CreateBooking_PaymentFails_ReleasesSeats()
    {
        // Подготовка
        _payment.Result = false;
        BookingRequest request = CreateRequest();

        // Действие
        Booking booking = _service.CreateBooking(request);

        // Проверка
        Assert.AreEqual(
            BookingStatus.PaymentFailed, booking.Status);
        Assert.AreEqual(1, _payment.ChargeCalls);
        Assert.AreEqual(1, _bookings.SaveCalls);
        Assert.AreEqual(booking, _bookings.SavedBooking);
        AssertSeatsRestored();
    }

    /// <summary>
    /// Проверяет возврат мест при сбое платёжного сервиса
    /// </summary>
    [TestMethod]
    public void CreateBooking_PaymentThrows_ReleasesSeats()
    {
        // Подготовка
        var expected = new InvalidOperationException(
            "Сбой оплаты");
        _payment.Error = expected;
        BookingRequest request = CreateRequest();

        // Действие
        InvalidOperationException actual =
            Assert.ThrowsException<InvalidOperationException>(
                () => _service.CreateBooking(request));

        // Проверка
        Assert.AreSame(expected, actual);
        Assert.AreEqual(1, _payment.ChargeCalls);
        Assert.AreEqual(0, _bookings.SaveCalls);
        Assert.IsNull(_bookings.SavedBooking);
        AssertSeatsRestored();
    }

    /// <summary>
    /// Проверяет, что сбой освобождения не скрывает сбой оплаты
    /// </summary>
    [TestMethod]
    public void CreateBooking_ReleaseThrows_KeepsPaymentError()
    {
        // Подготовка
        var paymentError = new ReadOnlyPaymentException();
        var releaseError = new InvalidOperationException(
            "Места");
        _payment.Error = paymentError;
        _events.ReleaseError = releaseError;
        BookingRequest request = CreateRequest();

        // Действие
        ReadOnlyPaymentException actual =
            Assert.ThrowsExactly<ReadOnlyPaymentException>(
                () => _service.CreateBooking(request));

        // Проверка
        Assert.AreSame(paymentError, actual);
        Assert.AreEqual(1, _events.ReleaseCalls);
        Assert.AreEqual(0, _bookings.SaveCalls);
    }

    /// <summary>
    /// Имитирует ошибку оплаты с неизменяемыми данными
    /// </summary>
    private sealed class ReadOnlyPaymentException
        : InvalidOperationException
    {
        /// <inheritdoc />
        public override IDictionary Data { get; } =
            new ReadOnlyDictionary<string, object>(new
                Dictionary<string, object>());
    }

    /// <summary>
    /// Проверяет валидацию до обращения к зависимостям
    /// </summary>
    [TestMethod]
    [DataRow(0, "customer@example.test", 125)]
    [DataRow(1, "", 125)]
    [DataRow(1, "customer", 125)]
    [DataRow(1, "customer@example.test", 0)]
    [DataRow(1, "customer@example.test", -1)]
    public void CreateBooking_InvalidRequest_SkipsDependencies(
        int eventId, string email, int price)
    {
        // Подготовка
        var request = new BookingRequest(
            eventId, email, 2, price);

        // Действие и проверка
        Assert.Throws<ArgumentException>(
            () => _service.CreateBooking(request));
        AssertNoLookupOrBookingActions();
    }

    /// <summary>
    /// Проверяет отклонение отсутствующего запроса
    /// </summary>
    [TestMethod]
    public void CreateBooking_NullRequest_SkipsDependencies()
    {
        // Подготовка
        BookingRequest? request = null;

        // Действие и проверка
        Assert.ThrowsExactly<ArgumentNullException>(
            () => _service.CreateBooking(request!));
        AssertNoLookupOrBookingActions();
    }

    /// <summary>
    /// Проверяет, что зависимости не использовались
    /// </summary>
    private void AssertNoLookupOrBookingActions()
    {
        Assert.AreEqual(0, _events.ExistsCalls);
        Assert.AreEqual(0, _events.AvailabilityCalls);
        AssertNoBookingActions();
    }

    /// <summary>
    /// Проверяет отклонение недопустимого количества мест
    /// </summary>
    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(11)]
    [DataRow(100)]
    public void CreateBooking_InvalidSeatsCount_RejectsRequest(
        int seatsCount)
    {
        // Подготовка
        BookingRequest request = CreateRequest() with
        {
            SeatsCount = seatsCount
        };

        // Действие и проверка
        ArgumentOutOfRangeException error =
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => _service.CreateBooking(request));
        Assert.AreEqual(
            nameof(request.SeatsCount), error.ParamName);
        AssertNoLookupOrBookingActions();
    }

    /// <summary>
    /// Проверяет отказ для отсутствующего мероприятия
    /// </summary>
    [TestMethod]
    public void CreateBooking_EventMissing_RejectsRequest()
    {
        // Подготовка
        _events.EventExists = false;
        BookingRequest request = CreateRequest();

        // Действие и проверка
        Assert.ThrowsExactly<InvalidOperationException>(
            () => _service.CreateBooking(request));
        Assert.AreEqual(1, _events.ExistsCalls);
        Assert.AreEqual(0, _events.AvailabilityCalls);
        AssertNoBookingActions();
    }

    /// <summary>
    /// Проверяет отклонение запроса при недостатке мест
    /// </summary>
    [TestMethod]
    public void CreateBooking_NotEnoughSeats_RejectsRequest()
    {
        // Подготовка
        _events.AvailableSeats = 1;
        BookingRequest request = CreateRequest();

        // Действие и проверка
        Assert.ThrowsExactly<InvalidOperationException>(
            () => _service.CreateBooking(request));
        Assert.AreEqual(1, _events.ExistsCalls);
        Assert.AreEqual(1, _events.AvailabilityCalls);
        AssertNoBookingActions();
    }

    /// <summary>
    /// Проверяет умножение количества мест на цену
    /// </summary>
    [DataTestMethod]
    [DataRow(1, 100, 100)]
    [DataRow(2, 500, 1000)]
    [DataRow(3, 1250, 3750)]
    [DataRow(10, 1, 10)]
    public void CreateBooking_ValidPrice_CalculatesTotal(
        int seatsCount, int price, int expectedTotal)
    {
        // Подготовка
        BookingRequest request = CreateRequest() with
        {
            SeatsCount = seatsCount,
            PricePerSeat = price
        };

        // Действие
        Booking booking = _service.CreateBooking(request);

        // Проверка
        Assert.AreEqual(
            (decimal)expectedTotal, booking.TotalAmount);
    }

    /// <summary>
    /// Проверяет порядок операций и восстановление мест
    /// </summary>
    private void AssertSeatsRestored()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                nameof(IEventRepository.ReserveSeats),
                nameof(IEventRepository.ReleaseSeats)
            },
            _events.SeatActions);
        Assert.AreEqual(1, _events.ReserveCalls);
        Assert.AreEqual(1, _events.ReleaseCalls);
        Assert.AreEqual(10, _events.AvailableSeats);
    }

    /// <summary>
    /// Проверяет отсутствие резервирования, оплаты и сохранения
    /// </summary>
    private void AssertNoBookingActions()
    {
        Assert.AreEqual(0, _events.ReserveCalls);
        Assert.AreEqual(0, _events.ReleaseCalls);
        Assert.AreEqual(0, _payment.ChargeCalls);
        Assert.AreEqual(0, _bookings.SaveCalls);
        Assert.AreEqual(0, _clock.ReadCalls);
        Assert.AreEqual(0, _idGenerator.CreateCalls);
    }

    /// <summary>
    /// Создаёт корректный запрос для подстановки в тестах
    /// </summary>
    private static BookingRequest CreateRequest()
    {
        return new(1, "customer@example.test", 2, 125m);
    }

    /// <summary>
    /// Хранит остаток мест и счётчики операций
    /// </summary>
    private sealed class FakeEventRepository : IEventRepository
    {
        /// <summary>
        /// Задаёт существование мероприятия
        /// </summary>
        public bool EventExists { get; set; } = true;

        /// <summary>
        /// Остаток мест до и после изменений
        /// </summary>
        public int AvailableSeats { get; set; } = 10;

        /// <summary>
        /// Число проверок существования мероприятия
        /// </summary>
        public int ExistsCalls { get; private set; }

        /// <summary>
        /// Число запросов остатка мест
        /// </summary>
        public int AvailabilityCalls { get; private set; }

        /// <summary>
        /// Число попыток резервирования мест
        /// </summary>
        public int ReserveCalls { get; private set; }

        /// <summary>
        /// Число попыток освобождения мест
        /// </summary>
        public int ReleaseCalls { get; private set; }

        /// <summary>
        /// Порядок операций с местами
        /// </summary>
        public List<string> SeatActions { get; } = new();

        /// <inheritdoc />
        public bool Exists(int eventId)
        {
            ExistsCalls++;
            return EventExists;
        }

        /// <inheritdoc />
        public int GetAvailableSeats(int eventId)
        {
            AvailabilityCalls++;
            return AvailableSeats;
        }

        /// <inheritdoc />
        public void ReserveSeats(int eventId, int seatsCount)
        {
            ReserveCalls++;
            SeatActions.Add(nameof(ReserveSeats));
            AvailableSeats -= seatsCount;
        }

        /// <summary>
        /// Исключение для имитации сбоя освобождения
        /// </summary>
        public Exception? ReleaseError { get; set; }

        /// <inheritdoc />
        public void ReleaseSeats(int eventId, int seatsCount)
        {
            ReleaseCalls++;
            SeatActions.Add(nameof(ReleaseSeats));
            if (ReleaseError is not null)
            {
                throw ReleaseError;
            }

            AvailableSeats += seatsCount;
        }
    }

    /// <summary>
    /// Запоминает сохранённое бронирование
    /// </summary>
    private sealed class FakeBookingRepository
        : IBookingRepository
    {
        /// <summary>
        /// Последнее сохранённое бронирование
        /// </summary>
        public Booking? SavedBooking { get; private set; }

        /// <summary>
        /// Число сохранений
        /// </summary>
        public int SaveCalls { get; private set; }

        /// <inheritdoc />
        public void Save(Booking booking)
        {
            SaveCalls++;
            SavedBooking = booking;
        }
    }

    /// <summary>
    /// Возвращает заданный результат или выбрасывает исключение
    /// </summary>
    private sealed class FakePaymentService : IPaymentService
    {
        /// <summary>
        /// Заданный результат оплаты
        /// </summary>
        public bool Result { get; set; } = true;

        /// <summary>
        /// Исключение для имитации сбоя платежа
        /// </summary>
        public Exception? Error { get; set; }

        /// <summary>
        /// Число вызовов оплаты
        /// </summary>
        public int ChargeCalls { get; private set; }

        /// <summary>
        /// Последняя сумма оплаты
        /// </summary>
        public decimal LastAmount { get; private set; }

        /// <inheritdoc />
        public bool Charge(string customerEmail, decimal amount)
        {
            ChargeCalls++;
            LastAmount = amount;

            if (Error is not null)
            {
                throw Error;
            }

            return Result;
        }
    }

    /// <summary>
    /// Возвращает фиксированное время
    /// </summary>
    private sealed class FakeClock(DateTimeOffset utcNow)
        : IClock
    {
        /// <summary>
        /// Число обращений за временем
        /// </summary>
        public int ReadCalls { get; private set; }

        /// <inheritdoc />
        public DateTimeOffset UtcNow
        {
            get
            {
                ReadCalls++;
                return utcNow;
            }
        }
    }

    /// <summary>
    /// Возвращает фиксированный идентификатор
    /// </summary>
    private sealed class FakeBookingIdGenerator(Guid id)
        : IBookingIdGenerator
    {
        /// <summary>
        /// Число запросов идентификатора
        /// </summary>
        public int CreateCalls { get; private set; }

        /// <inheritdoc />
        public Guid Create()
        {
            CreateCalls++;
            return id;
        }
    }
}
