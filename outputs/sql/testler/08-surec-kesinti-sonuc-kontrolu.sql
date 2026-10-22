-- Salt okunur denetim: 21 kontrolle tamamlanan süreç/kesinti testi.
USE [SolidWorksAutomation_ApiTest_20261008_04];
GO
IF (SELECT COUNT_BIG(*) FROM dbo.Documents) <> 6
 OR (SELECT COUNT(DISTINCT Etiket) FROM dbo.Documents) <> 6
 OR (SELECT COUNT(DISTINCT ID) FROM dbo.Documents) <> 6
 OR EXISTS (SELECT 1 FROM dbo.Documents WHERE DosyaAdi=N'Kesilen_Yazma.SLDPRT')
    THROW 51960, N'Süreç/kesinti testi kayıtları beklenen sonuçla eşleşmiyor.', 1;
SELECT ID, Etiket, DosyaAdi, Tarih FROM dbo.Documents ORDER BY ID;
SELECT COUNT_BIG(*) AS KayitSayisi, COUNT(DISTINCT ID) AS BenzersizID,
    COUNT(DISTINCT Etiket) AS BenzersizEtiket FROM dbo.Documents;
SELECT name AS Sayac, last_used_value AS SonKullanilanDeger FROM sys.sequences;
GO
USE [SolidWorksAutomation];
GO
SELECT COUNT_BIG(*) AS AnaVeritabaniKayitSayisi FROM dbo.Documents;
GO
