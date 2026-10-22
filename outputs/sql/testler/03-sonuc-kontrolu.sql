USE [SolidWorksAutomation_EtiketTest_20261008];
GO
SET NOCOUNT ON;
IF (SELECT COUNT(*) FROM dbo.TestCalismalari WHERE Bitis IS NOT NULL) <> 2
    THROW 51910, N'Iki isci tamamlanmadi.', 1;
IF NOT EXISTS
 (SELECT 1 FROM dbo.TestCalismalari a JOIN dbo.TestCalismalari b ON a.Isci=1 AND b.Isci=2
  WHERE a.Oturum<>b.Oturum AND a.Baslangic<b.Bitis AND b.Baslangic<a.Bitis)
    THROW 51911, N'Ayri SQL oturumlarinda gercek eszamanlilik dogrulanamadi.', 1;
IF (SELECT COUNT(*) FROM dbo.Documents) <> 203
 OR (SELECT COUNT(DISTINCT Etiket) FROM dbo.Documents) <> 203
 OR (SELECT COUNT(DISTINCT ID) FROM dbo.Documents) <> 203
 OR (SELECT MAX(ID) FROM dbo.Documents) <> 203
 OR (SELECT COUNT(*) FROM dbo.Documents WHERE LEFT(Etiket,1)=N'P') <> 102
 OR (SELECT COUNT(*) FROM dbo.Documents WHERE LEFT(Etiket,1)=N'M') <> 101
    THROW 51912, N'Eszamanli kayitlar veya ayri sayaclar hatali.', 1;
SELECT N'200 eszamanli istek gecti; 203 benzersiz ID/etiket, P=102, M=101.' AS Sonuc;
SELECT * FROM dbo.TestCalismalari;

-- Kayit basarisiz oldugunda transaction geriye alinmali; numara tekrar kullanilmamali.
ALTER TABLE dbo.Documents ADD CONSTRAINT CK_Test_KayitHatasi CHECK (DosyaAdi <> N'Hata.SLDPRT');
BEGIN TRY
    EXEC dbo.BelgeKaydet N'P', N'Hata.SLDPRT';
    THROW 51913, N'Zorlanan kayit hatasi olusmadi.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 547 THROW;
END CATCH;
IF @@TRANCOUNT <> 0 OR (SELECT COUNT(*) FROM dbo.Documents) <> 203
    THROW 51914, N'Kayit hatasi transaction veya veri birakti.', 1;
EXEC dbo.BelgeKaydet N'P', N'HatadanSonra.SLDPRT';
IF NOT EXISTS (SELECT 1 FROM dbo.Documents WHERE Etiket=N'P00104' AND DosyaAdi=N'HatadanSonra.SLDPRT')
    THROW 51915, N'Hatadan sonra sayac devam etmiyor.', 1;

-- 5 hane sinirinda kesilme veya basa donus olmamali. Yalnizca test sayaci degistirilir.
ALTER SEQUENCE dbo.ParcaSayaci RESTART WITH 99999;
EXEC dbo.BelgeKaydet N'P', N'Sinir1.SLDPRT';
EXEC dbo.BelgeKaydet N'P', N'Sinir2.SLDPRT';
IF NOT EXISTS (SELECT 1 FROM dbo.Documents WHERE Etiket=N'P99999')
 OR NOT EXISTS (SELECT 1 FROM dbo.Documents WHERE Etiket=N'P100000')
    THROW 51916, N'Bes hane siniri hatali.', 1;
SELECT N'Kayit hatasi/rollback, numara tekrar kullanmama ve 99999 siniri testleri gecti.' AS Sonuc;
GO

USE [SolidWorksAutomation];
GO
IF (SELECT COUNT(*) FROM dbo.Documents) <> 0
    THROW 51917, N'Ana veritabanina test kaydi eklenmis.', 1;
SELECT COUNT(*) AS AnaKayitSayisi FROM dbo.Documents;
SELECT name AS Sayac, current_value AS MevcutDeger, start_value AS Baslangic,
       is_cached AS Onbellek, is_cycling AS BasaDonus FROM sys.sequences;
GO
