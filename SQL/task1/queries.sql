USE SteamPractice;

IF DB_NAME() <> N'SteamPractice'
    THROW 50003, N'Выбрана не учебная база', 1;

SET XACT_ABORT ON;
SET NOCOUNT ON;

SELECT Title, Price, ReleasedAt
FROM dbo.Games
WHERE IsAvailable = 1 AND Price < 15.00
ORDER BY Price, AppId;

BEGIN TRANSACTION;

UPDATE dbo.Games
SET Price = 7.99
OUTPUT inserted.Title, deleted.Price AS OldPrice,
       inserted.Price AS NewPrice
WHERE AppId = 910001;

DELETE FROM dbo.Reviews
OUTPUT deleted.ReviewId, deleted.Body
WHERE ReviewId = '00000000-0000-0000-0000-000000000002';

ROLLBACK TRANSACTION;

SELECT s.StudioId, s.Name, COUNT(*) AS GameCount,
       CAST(AVG(g.Price) AS decimal(10, 2)) AS AveragePrice
FROM dbo.Studios AS s
INNER JOIN dbo.Games AS g ON g.StudioId = s.StudioId
GROUP BY s.StudioId, s.Name
ORDER BY s.Name, s.StudioId;

SELECT s.Name AS Studio, g.Title AS Game
FROM dbo.Studios AS s
LEFT JOIN dbo.Games AS g ON g.StudioId = s.StudioId
ORDER BY s.StudioId, g.AppId;

SELECT g.Title AS Game, r.Body AS Review, r.IsRecommended
FROM dbo.Reviews AS r
RIGHT JOIN dbo.Games AS g ON g.GameId = r.GameId
ORDER BY g.AppId, r.PostedAt, r.ReviewId;

SELECT g.AppId, g.Title
FROM dbo.Games AS g
INNER JOIN dbo.GameGenres AS gg ON gg.GameId = g.GameId
INNER JOIN dbo.Genres AS ge ON ge.GenreId = gg.GenreId
WHERE ge.Name = N'Приключения'
INTERSECT
SELECT g.AppId, g.Title
FROM dbo.Games AS g
INNER JOIN dbo.GameGenres AS gg ON gg.GameId = g.GameId
INNER JOIN dbo.Genres AS ge ON ge.GenreId = gg.GenreId
WHERE ge.Name = N'Головоломки'
ORDER BY AppId;
