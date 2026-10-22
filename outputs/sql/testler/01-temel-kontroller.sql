-- Yalnizca bos ve ayri test veritabaninda, 002 ve 003 kurulduktan sonra.
USE [SolidWorksAutomation_EtiketTest_20261008];
GO
SET NOCOUNT ON;
IF EXISTS (SELECT 1 FROM dbo.Documents)
    THROW 51900, N'Test veritabani bos olmali.', 1;
EXEC dbo.BelgeKaydet N'P', N'Mil.SLDPRT';
EXEC dbo.BelgeKaydet N'M', N'AnaMontaj.SLDASM';
EXEC dbo.BelgeKaydet N'P', N'Mil.SLDPRT';
IF NOT EXISTS (SELECT 1 FROM dbo.Documents WHERE ID=1 AND Etiket=N'P00001')
 OR NOT EXISTS (SELECT 1 FROM dbo.Documents WHERE ID=2 AND Etiket=N'M00001')
 OR NOT EXISTS (SELECT 1 FROM dbo.Documents WHERE ID=3 AND Etiket=N'P00002')
    THROW 51901, N'Ilk etiketler veya global ID hatali.', 1;

BEGIN TRY
    EXEC dbo.BelgeKaydet N'X', N'Hata.SLDPRT';
    THROW 51902, N'Gecersiz tur kabul edildi.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 51004 THROW;
END CATCH;
BEGIN TRY
    EXEC dbo.BelgeKaydet N'P', N'   ';
    THROW 51903, N'Bos ad kabul edildi.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 51005 THROW;
END CATCH;
BEGIN TRY
    EXEC dbo.BelgeKaydet N'P', N'Hata.SLDASM';
    THROW 51904, N'Yanlis uzanti kabul edildi.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 51006 THROW;
END CATCH;
DECLARE @UzunAd nvarchar(max) = REPLICATE(N'a', 256) + N'.SLDPRT';
BEGIN TRY
    EXEC dbo.BelgeKaydet N'P', @UzunAd;
    THROW 51905, N'Uzun ad kabul edildi.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 51005 THROW;
END CATCH;
IF (SELECT COUNT(*) FROM dbo.Documents) <> 3
 OR (SELECT CONVERT(bigint,current_value) FROM sys.sequences WHERE name=N'ParcaSayaci') <> 2
 OR (SELECT CONVERT(bigint,current_value) FROM sys.sequences WHERE name=N'MontajSayaci') <> 1
    THROW 51906, N'Gecersiz istek kaydi veya sayaci degistirdi.', 1;

CREATE TABLE dbo.TestCalismalari
    (Isci int PRIMARY KEY, Oturum int NOT NULL, Baslangic datetime2(7), Bitis datetime2(7));
SELECT N'Temel testler gecti: ilk P/M, global ID, tekrar dosya adi ve 4 gecersiz istek.' AS Sonuc;
GO
