-- SSMS'de iki ayri sorgu sekmesinde calistirin. Ikinci sekmede @Isci = 2 yapin.
-- Ilk sorgudan sonra ikinci sorguyu 60 saniye icinde baslatin.
USE [SolidWorksAutomation_EtiketTest_20261008];
GO
SET NOCOUNT ON;
DECLARE @Isci int = 1;
INSERT dbo.TestCalismalari (Isci, Oturum) VALUES (@Isci, @@SPID);
DECLARE @Bekleme int = 0;
WHILE (SELECT COUNT(*) FROM dbo.TestCalismalari) < 2
BEGIN
    IF @Bekleme >= 600 THROW 51920, N'Ikinci test iscisi baslatilmadi.', 1;
    WAITFOR DELAY '00:00:00.10';
    SET @Bekleme += 1;
END;
UPDATE dbo.TestCalismalari SET Baslangic=SYSUTCDATETIME() WHERE Isci=@Isci;
DECLARE @i int = 0, @Tur nvarchar(1), @Ad nvarchar(255);
WHILE @i < 100
BEGIN
    SET @Tur = CASE WHEN @i % 2 = 0 THEN N'P' ELSE N'M' END;
    SET @Ad = CASE WHEN @Tur = N'P' THEN N'Eszamanli.SLDPRT' ELSE N'Eszamanli.SLDASM' END;
    EXEC dbo.BelgeKaydet @Tur, @Ad;
    WAITFOR DELAY '00:00:00.10';
    SET @i += 1;
END;
UPDATE dbo.TestCalismalari SET Bitis=SYSUTCDATETIME() WHERE Isci=@Isci;
SELECT @Isci AS TamamlananIsci;
GO
