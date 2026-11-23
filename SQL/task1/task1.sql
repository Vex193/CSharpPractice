CREATE DATABASE SteamPractice;
GO

USE SteamPractice;
GO

IF DB_NAME() <> N'SteamPractice'
    THROW 50002, N'Выбрана не учебная база', 1;

SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

CREATE TABLE dbo.Studios
(
    StudioId int NOT NULL CONSTRAINT PK_Studios PRIMARY KEY,
    Name nvarchar(100) NOT NULL
);

CREATE TABLE dbo.Games
(
    GameId int NOT NULL CONSTRAINT PK_Games PRIMARY KEY,
    AppId int NOT NULL CONSTRAINT CK_Games_AppId CHECK (AppId > 0),
    StudioId int NOT NULL,
    Title nvarchar(120) NOT NULL,
    Price decimal(10, 2) NOT NULL,
    ReleasedAt datetime2(0) NOT NULL,
    IsAvailable bit NOT NULL,
    CONSTRAINT FK_Games_Studios FOREIGN KEY (StudioId)
        REFERENCES dbo.Studios (StudioId),
    CONSTRAINT CK_Games_Price CHECK (Price >= 0)
);

CREATE UNIQUE INDEX UX_Games_AppId ON dbo.Games (AppId);

CREATE TABLE dbo.Genres
(
    GenreId int NOT NULL CONSTRAINT PK_Genres PRIMARY KEY,
    Name nvarchar(60) NOT NULL CONSTRAINT UQ_Genres_Name UNIQUE
);

CREATE TABLE dbo.GameGenres
(
    GameId int NOT NULL,
    GenreId int NOT NULL,
    CONSTRAINT PK_GameGenres PRIMARY KEY (GameId, GenreId),
    CONSTRAINT FK_GameGenres_Games FOREIGN KEY (GameId)
        REFERENCES dbo.Games (GameId),
    CONSTRAINT FK_GameGenres_Genres FOREIGN KEY (GenreId)
        REFERENCES dbo.Genres (GenreId)
);

CREATE TABLE dbo.Reviews
(
    ReviewId uniqueidentifier NOT NULL
        CONSTRAINT PK_Reviews PRIMARY KEY
        CONSTRAINT DF_Reviews_ReviewId DEFAULT NEWSEQUENTIALID(),
    GameId int NOT NULL,
    Body nvarchar(500) NOT NULL,
    PostedAt datetime2(0) NOT NULL,
    IsRecommended bit NOT NULL,
    CONSTRAINT FK_Reviews_Games FOREIGN KEY (GameId)
        REFERENCES dbo.Games (GameId)
);

INSERT INTO dbo.Studios (StudioId, Name)
VALUES (1, N'Northlight Lab'), (2, N'Pixel Harbor'),
       (3, N'Quiet Orbit');

INSERT INTO dbo.Games
    (GameId, AppId, StudioId, Title, Price, ReleasedAt, IsAvailable)
VALUES (1, 910001, 1, N'Orbit Courier', 9.99, '2025-01-10T10:00:00', 1),
       (2, 910002, 1, N'Glass Harbor', 19.99, '2025-03-21T15:00:00', 1),
       (3, 910003, 2, N'Night Circuit', 0.00, '2024-11-01T12:00:00', 1),
       (4, 910004, 2, N'Drift Signal', 4.99, '2024-06-01T11:00:00', 0);

INSERT INTO dbo.Genres (GenreId, Name)
VALUES (1, N'Приключения'), (2, N'Головоломки'), (3, N'Гонки');

INSERT INTO dbo.GameGenres (GameId, GenreId)
VALUES (1, 1), (1, 2), (2, 1), (2, 2), (3, 3), (4, 2);

INSERT INTO dbo.Reviews (ReviewId, GameId, Body, PostedAt, IsRecommended)
VALUES
    ('00000000-0000-0000-0000-000000000001', 1,
     N'Хорошие головоломки', '2025-01-11T11:00:00', 1),
    ('00000000-0000-0000-0000-000000000002', 1,
     N'Управление не понравилось', '2025-01-12T12:00:00', 0),
    ('00000000-0000-0000-0000-000000000003', 2,
     N'Интересный сюжет', '2025-03-22T09:00:00', 1);

COMMIT TRANSACTION;
