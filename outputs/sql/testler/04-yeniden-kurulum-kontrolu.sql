-- 03 testinden SONRA, 003 motor kurulumunu test veritabaninda tekrar calistirin.
-- 003 icindeki USE satiri test veritabanini gostermeli.
USE [SolidWorksAutomation_EtiketTest_20261008];
GO
IF (SELECT CONVERT(bigint,current_value) FROM sys.sequences WHERE name=N'ParcaSayaci') <> 100000
 OR (SELECT CONVERT(bigint,current_value) FROM sys.sequences WHERE name=N'MontajSayaci') <> 101
    THROW 51930, N'Yeniden kurulum sayac degerlerini degistirdi.', 1;
EXEC dbo.BelgeKaydet N'P', N'YenidenKurulum.SLDPRT';
EXEC dbo.BelgeKaydet N'M', N'YenidenKurulum.SLDASM';
IF NOT EXISTS (SELECT 1 FROM dbo.Documents WHERE Etiket=N'P100001')
 OR NOT EXISTS (SELECT 1 FROM dbo.Documents WHERE Etiket=N'M00102')
    THROW 51931, N'Yeniden kurulumdan sonra siradaki etiketler hatali.', 1;
SELECT N'Yeniden kurulum gecti: P100001 ve M00102; sayaclar sifirlanmadi.' AS Sonuc;
GO
USE [SolidWorksAutomation];
GO
SELECT COUNT(*) AS AnaKayitSayisi FROM dbo.Documents;
SELECT name AS Sayac, last_used_value AS SonKullanilanDeger FROM sys.sequences;
GO
