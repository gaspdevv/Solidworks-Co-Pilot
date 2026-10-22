-- C# istemcisi gerçek SQL testinin sonuçları. Veri silmez, sayaç değiştirmez.
USE [SolidWorksAutomation_ApiTest_20261008_03];
GO
IF (SELECT COUNT_BIG(*) FROM dbo.Documents) <> 104
 OR (SELECT COUNT(DISTINCT ID) FROM dbo.Documents) <> 104
 OR (SELECT COUNT(DISTINCT Etiket) FROM dbo.Documents) <> 104
 OR (SELECT COUNT_BIG(*) FROM dbo.Documents WHERE Etiket LIKE N'P%') <> 53
 OR (SELECT COUNT_BIG(*) FROM dbo.Documents WHERE Etiket LIKE N'M%') <> 51
    THROW 51950, N'C# istemcisi test kayıtları beklenen sonuçla eşleşmiyor.', 1;
SELECT COUNT_BIG(*) AS KayitSayisi, COUNT(DISTINCT ID) AS BenzersizID,
    COUNT(DISTINCT Etiket) AS BenzersizEtiket FROM dbo.Documents;
SELECT LEFT(Etiket,1) AS Tur, COUNT_BIG(*) AS KayitSayisi FROM dbo.Documents GROUP BY LEFT(Etiket,1);
SELECT ID, Etiket, DosyaAdi FROM dbo.Documents WHERE ID = 104;
GO
USE [SolidWorksAutomation];
GO
SELECT COUNT_BIG(*) AS AnaVeritabaniKayitSayisi FROM dbo.Documents;
GO
