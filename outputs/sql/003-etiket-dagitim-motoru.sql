-- TARİHSEL SÖZLEŞME: Yeni kurulum/geçiş için 004-dosya-adi-rezervasyon.sql kullanın.
-- Once 001 ve 002 kurulumlarini calistirin. Tekrar calistirma sayaclari sifirlamaz.
USE [SolidWorksAutomation];
GO

SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.Documents', N'U') IS NULL
        THROW 51000, N'Documents tablosu bulunamadi.', 1;

    IF OBJECT_ID(N'dbo.ParcaSayaci', N'SO') IS NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.Documents WHERE Etiket LIKE N'P%')
            THROW 51001, N'Parca kayitlari var; eksik sayac otomatik baslatilamaz.', 1;
        CREATE SEQUENCE dbo.ParcaSayaci AS bigint
            START WITH 1 INCREMENT BY 1 MINVALUE 1 NO MAXVALUE NO CYCLE NO CACHE;
    END;

    IF OBJECT_ID(N'dbo.MontajSayaci', N'SO') IS NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.Documents WHERE Etiket LIKE N'M%')
            THROW 51002, N'Montaj kayitlari var; eksik sayac otomatik baslatilamaz.', 1;
        CREATE SEQUENCE dbo.MontajSayaci AS bigint
            START WITH 1 INCREMENT BY 1 MINVALUE 1 NO MAXVALUE NO CYCLE NO CACHE;
    END;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

CREATE OR ALTER PROCEDURE dbo.BelgeKaydet
    @BelgeTuru nvarchar(max), -- P = Parca, M = Montaj. Documents'a kolon eklenmez.
    @DosyaAdi nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Prosedur kendi islemini tamamlar; ic ice transaction kabul etmez.
    IF @@TRANCOUNT <> 0
        THROW 51003, N'BelgeKaydet acik bir transaction icinde cagrilamaz.', 1;
    IF @BelgeTuru IS NULL OR DATALENGTH(@BelgeTuru) <> 2
       OR @BelgeTuru COLLATE Latin1_General_100_BIN2 NOT IN (N'P', N'M')
        THROW 51004, N'Belge turu P veya M olmalidir.', 1;
    IF @DosyaAdi IS NULL OR LEN(LTRIM(RTRIM(@DosyaAdi))) = 0
       OR DATALENGTH(@DosyaAdi) > 510
        THROW 51005, N'Dosya adi bos olamaz ve 255 karakteri asamaz.', 1;
    IF (@BelgeTuru = N'P' AND UPPER(RIGHT(@DosyaAdi, 7)) <> N'.SLDPRT')
       OR (@BelgeTuru = N'M' AND UPPER(RIGHT(@DosyaAdi, 7)) <> N'.SLDASM')
        THROW 51006, N'Dosya uzantisi belge turu ile uyusmuyor.', 1;
    IF OBJECT_ID(N'dbo.ParcaSayaci', N'SO') IS NULL
       OR OBJECT_ID(N'dbo.MontajSayaci', N'SO') IS NULL
        THROW 51007, N'Etiket sayaclari kurulmamıs.', 1;

    DECLARE @Numara bigint, @Rakamlar nvarchar(20), @Etiket nvarchar(32);
    DECLARE @Kayit TABLE
        (ID bigint, Etiket nvarchar(32), DosyaAdi nvarchar(255), Tarih datetime2(7));

    BEGIN TRY
        BEGIN TRANSACTION;
        IF @BelgeTuru = N'P'
            SET @Numara = NEXT VALUE FOR dbo.ParcaSayaci;
        ELSE
            SET @Numara = NEXT VALUE FOR dbo.MontajSayaci;

        SET @Rakamlar = CONVERT(nvarchar(20), @Numara);
        -- Bes hane minimumdur: 99999 sonrasi P100000/M100000 olur, basa donmez.
        SET @Etiket = @BelgeTuru
            + REPLICATE(N'0', CASE WHEN LEN(@Rakamlar) < 5 THEN 5 - LEN(@Rakamlar) ELSE 0 END)
            + @Rakamlar;

        INSERT dbo.Documents (Etiket, DosyaAdi)
            OUTPUT inserted.ID, inserted.Etiket, inserted.DosyaAdi, inserted.Tarih INTO @Kayit
            VALUES (@Etiket, @DosyaAdi);
        COMMIT TRANSACTION;

        -- Basari sonucu sadece COMMIT tamamlandiktan sonra dondurulur.
        SELECT ID, Etiket, DosyaAdi, Tarih FROM @Kayit;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
