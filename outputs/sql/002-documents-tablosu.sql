-- TARİHSEL SÖZLEŞME: Yeni kurulum/geçiş için 004-dosya-adi-rezervasyon.sql kullanın.
-- SSMS'de hedef SQL Server baglantisinda calistirin.
-- Var olan tabloyu silmez veya degistirmez. Ornek kayit eklemez.
USE [SolidWorksAutomation];
GO

IF OBJECT_ID(N'dbo.Documents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Documents
    (
        ID bigint IDENTITY(1, 1) NOT NULL,
        Etiket nvarchar(32) NOT NULL,
        DosyaAdi nvarchar(255) NOT NULL,
        Tarih datetime2(7) NOT NULL
            CONSTRAINT DF_Documents_Tarih DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_Documents PRIMARY KEY (ID),
        CONSTRAINT UQ_Documents_Etiket UNIQUE (Etiket)
    );
END;
GO

-- Kurulum kontrolu: dort sutun, anahtarlar ve UTC tarih varsayilani.
SELECT c.name AS Sutun, t.name AS VeriTuru,
       c.max_length AS BaytUzunlugu, c.is_nullable AS BosDeger,
       c.is_identity AS OtomatikID, d.definition AS Varsayilan
FROM sys.columns AS c
JOIN sys.types AS t ON c.user_type_id = t.user_type_id
LEFT JOIN sys.default_constraints AS d ON c.default_object_id = d.object_id
WHERE c.object_id = OBJECT_ID(N'dbo.Documents', N'U')
ORDER BY c.column_id;

SELECT k.name AS Kisit, k.type_desc AS KisitTuru, c.name AS Sutun
FROM sys.key_constraints AS k
JOIN sys.index_columns AS i
  ON i.object_id = k.parent_object_id AND i.index_id = k.unique_index_id
JOIN sys.columns AS c
  ON c.object_id = i.object_id AND c.column_id = i.column_id
WHERE k.parent_object_id = OBJECT_ID(N'dbo.Documents', N'U');

SELECT COUNT_BIG(*) AS KayitSayisi FROM dbo.Documents;
GO
