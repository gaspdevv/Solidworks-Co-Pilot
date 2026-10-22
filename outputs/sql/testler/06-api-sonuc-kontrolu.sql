USE [SolidWorksAutomation];
GO
IF (SELECT COUNT_BIG(*) FROM dbo.Documents) <> 0
 OR EXISTS (SELECT 1 FROM sys.sequences WHERE name IN (N'ParcaSayaci',N'MontajSayaci') AND last_used_value IS NOT NULL)
    THROW 51940, N'Ana veritabani testten etkilenmis.', 1;
SELECT COUNT_BIG(*) AS AnaKayitSayisi FROM dbo.Documents;
SELECT name AS Sayac, last_used_value AS SonKullanilanDeger FROM sys.sequences;
GO
USE [SolidWorksAutomation_ApiTest_20261008_02];
GO
IF (SELECT COUNT_BIG(*) FROM dbo.Documents) <> 104
 OR (SELECT COUNT(DISTINCT ID) FROM dbo.Documents) <> 104
 OR (SELECT COUNT(DISTINCT Etiket) FROM dbo.Documents) <> 104
    THROW 51941, N'API testinin kalici SQL kayitlari hatali.', 1;
SELECT COUNT_BIG(*) AS TestKayitSayisi, COUNT(DISTINCT ID) AS BenzersizID,
       COUNT(DISTINCT Etiket) AS BenzersizEtiket FROM dbo.Documents;
SELECT LEFT(Etiket,1) AS Tur, COUNT(*) AS KayitSayisi FROM dbo.Documents GROUP BY LEFT(Etiket,1);
SELECT ID, Etiket, DosyaAdi FROM dbo.Documents WHERE DosyaAdi = N'YenidenBaslatma.SLDPRT';
GO
