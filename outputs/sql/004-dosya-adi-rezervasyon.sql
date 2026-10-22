IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Rezervasyonlar') AND name=N'UX_Rezervasyonlar_AktifDosyaAdi') THROW 51114,N'005 kurulmuş ortamda eski 004 çalıştırılmaz; 005 kullanın.',1;
-- 004: yeni adlandırma ve rezervasyon akışı. SSMS'de HEDEF veritabanını seçin.
-- Yeni ortam: önce 001; ardından bu dosya. 002/003 eski sözleşmedir.
-- Kayıt içeren eski tabloyu dönüştürmez, sayaçları sıfırlamaz, test verisi eklemez.
SET XACT_ABORT ON;
IF DB_ID() <= 4 THROW 51110,N'Hedef uygulama veritabanını seçin.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @Kilit int;
 EXEC @Kilit=sys.sp_getapplock @Resource=N'Pronom-Sema-Gecisi',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
 IF @Kilit<0 THROW 51111,N'Kurulum kilidi alınamadı.',1;
 IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Rezervasyonlar') AND name=N'UX_Rezervasyonlar_AktifDosyaAdi') THROW 51114,N'005 kurulmuş ortamda eski 004 çalıştırılmaz; 005 kullanın.',1;
 IF COL_LENGTH(N'dbo.Documents',N'Etiket') IS NOT NULL
 BEGIN
  IF EXISTS(SELECT 1 FROM dbo.Documents WITH(TABLOCKX,HOLDLOCK))
   THROW 51112,N'Eski tabloda kayıt var. CAD dosyalarıyla birlikte ayrı geçiş planı gerekir; veri değiştirilmedi.',1;
  IF OBJECT_ID(N'dbo.BelgeKaydet',N'P') IS NOT NULL DROP PROCEDURE dbo.BelgeKaydet;
  IF OBJECT_ID(N'dbo.UQ_Documents_Etiket',N'UQ') IS NOT NULL ALTER TABLE dbo.Documents DROP CONSTRAINT UQ_Documents_Etiket;
  ALTER TABLE dbo.Documents DROP COLUMN Etiket;
 END;
 IF OBJECT_ID(N'dbo.Documents',N'U') IS NULL
 CREATE TABLE dbo.Documents(
  ID bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Documents PRIMARY KEY,
  DosyaAdi nvarchar(255) NOT NULL,
  Tarih datetime2(7) NOT NULL CONSTRAINT DF_Documents_Tarih DEFAULT SYSUTCDATETIME());
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Documents') AND name=N'UQ_Documents_DosyaAdi')
  ALTER TABLE dbo.Documents ADD CONSTRAINT UQ_Documents_DosyaAdi UNIQUE(DosyaAdi);
 IF OBJECT_ID(N'dbo.ParcaSayaci',N'SO') IS NULL
 BEGIN
  IF EXISTS(SELECT 1 FROM dbo.Documents WHERE DosyaAdi LIKE N'P%') THROW 51113,N'Parça kaydı var; eksik sayaç otomatik başlatılamaz.',1;
  IF OBJECT_ID(N'dbo.Rezervasyonlar',N'U') IS NOT NULL
   IF EXISTS(SELECT 1 FROM dbo.Rezervasyonlar WHERE DosyaAdi LIKE N'P%') THROW 51113,N'Parça rezervasyonu var; eksik sayaç başlatılamaz.',1;
  CREATE SEQUENCE dbo.ParcaSayaci AS bigint START WITH 1 INCREMENT BY 1 MINVALUE 1 NO MAXVALUE NO CYCLE NO CACHE;
 END;
 IF OBJECT_ID(N'dbo.MontajSayaci',N'SO') IS NULL
 BEGIN
  IF EXISTS(SELECT 1 FROM dbo.Documents WHERE DosyaAdi LIKE N'M%') THROW 51113,N'Montaj kaydı var; eksik sayaç otomatik başlatılamaz.',1;
  IF OBJECT_ID(N'dbo.Rezervasyonlar',N'U') IS NOT NULL
   IF EXISTS(SELECT 1 FROM dbo.Rezervasyonlar WHERE DosyaAdi LIKE N'M%') THROW 51113,N'Montaj rezervasyonu var; eksik sayaç başlatılamaz.',1;
  CREATE SEQUENCE dbo.MontajSayaci AS bigint START WITH 1 INCREMENT BY 1 MINVALUE 1 NO MAXVALUE NO CYCLE NO CACHE;
 END;
 IF OBJECT_ID(N'dbo.Rezervasyonlar',N'U') IS NULL
 CREATE TABLE dbo.Rezervasyonlar(
  IslemId uniqueidentifier NOT NULL CONSTRAINT PK_Rezervasyonlar PRIMARY KEY,
  DosyaAdi nvarchar(32) NOT NULL CONSTRAINT UQ_Rezervasyonlar_DosyaAdi UNIQUE,
  Durum tinyint NOT NULL CONSTRAINT CK_Rezervasyonlar_Durum CHECK(Durum IN(0,1,2)),
  Tarih datetime2(7) NOT NULL CONSTRAINT DF_Rezervasyonlar_Tarih DEFAULT SYSUTCDATETIME());

 EXEC(N'
CREATE OR ALTER PROCEDURE dbo.DosyaAdiAyir @IslemId uniqueidentifier,@BelgeTuru nvarchar(10)
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @@TRANCOUNT<>0 THROW 51103,N''İç içe işlem kabul edilmez.'',1;
 IF @IslemId IS NULL OR @IslemId=''00000000-0000-0000-0000-000000000000''
  OR @BelgeTuru IS NULL OR DATALENGTH(@BelgeTuru)<>2
  OR @BelgeTuru COLLATE Latin1_General_100_BIN2 NOT IN(N''P'',N''M'') THROW 51103,N''İşlem kimliği ve P/M türü gereklidir.'',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @Kilit int,@Kaynak nvarchar(255)=N''Pronom-Rezervasyon:''+CONVERT(nvarchar(36),@IslemId),@DosyaAdi nvarchar(32),@Numara bigint,@Rakam nvarchar(20);
  EXEC @Kilit=sys.sp_getapplock @Resource=@Kaynak,@LockMode=N''Exclusive'',@LockOwner=N''Transaction'',@LockTimeout=10000;
  IF @Kilit<0 THROW 51105,N''İşlem kilidi alınamadı.'',1;
  SELECT @DosyaAdi=DosyaAdi FROM dbo.Rezervasyonlar WHERE IslemId=@IslemId;
  IF @DosyaAdi IS NOT NULL
  BEGIN
   IF LEFT(@DosyaAdi,1)<>@BelgeTuru THROW 51104,N''Aynı işlem kimliği farklı tür için kullanılamaz.'',1;
  END
  ELSE
  BEGIN
   IF @BelgeTuru=N''P'' SET @Numara=NEXT VALUE FOR dbo.ParcaSayaci;
   ELSE SET @Numara=NEXT VALUE FOR dbo.MontajSayaci;
   SET @Rakam=CONVERT(nvarchar(20),@Numara);
   SET @DosyaAdi=@BelgeTuru+REPLICATE(N''0'',CASE WHEN LEN(@Rakam)<5 THEN 5-LEN(@Rakam) ELSE 0 END)+@Rakam
    +CASE WHEN @BelgeTuru=N''P'' THEN N''.SLDPRT'' ELSE N''.SLDASM'' END;
   IF EXISTS(SELECT 1 FROM dbo.Documents WHERE DosyaAdi=@DosyaAdi) THROW 51104,N''Üretilen ad zaten kayıtlı; sayaç incelenmelidir.'',1;
   INSERT dbo.Rezervasyonlar(IslemId,DosyaAdi,Durum)VALUES(@IslemId,@DosyaAdi,0);
  END;
  COMMIT TRANSACTION;
  SELECT IslemId,DosyaAdi,Durum,Tarih FROM dbo.Rezervasyonlar WHERE IslemId=@IslemId;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK TRANSACTION;THROW;
 END CATCH;
END;
');

 EXEC(N'
CREATE OR ALTER PROCEDURE dbo.RezervasyonOnayla @IslemId uniqueidentifier
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @@TRANCOUNT<>0 THROW 51103,N''İç içe işlem kabul edilmez.'',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @Durum tinyint,@DosyaAdi nvarchar(32);
  SELECT @Durum=Durum,@DosyaAdi=DosyaAdi FROM dbo.Rezervasyonlar WITH(UPDLOCK,HOLDLOCK) WHERE IslemId=@IslemId;
  IF @Durum IS NULL THROW 51101,N''İşlem bulunamadı.'',1;
  IF @Durum=2 THROW 51102,N''İptal edilmiş işlem onaylanamaz.'',1;
  DECLARE @Sonuc TABLE(ID bigint,DosyaAdi nvarchar(255),Tarih datetime2(7));
  IF @Durum=0
  BEGIN
   INSERT dbo.Documents(DosyaAdi) OUTPUT inserted.ID,inserted.DosyaAdi,inserted.Tarih INTO @Sonuc VALUES(@DosyaAdi);
   UPDATE dbo.Rezervasyonlar SET Durum=1 WHERE IslemId=@IslemId;
  END
  ELSE INSERT @Sonuc SELECT ID,DosyaAdi,Tarih FROM dbo.Documents WHERE DosyaAdi=@DosyaAdi;
  IF NOT EXISTS(SELECT 1 FROM @Sonuc) THROW 51105,N''Onaylı belge kaydı eksik.'',1;
  COMMIT TRANSACTION;
  SELECT ID,DosyaAdi,Tarih FROM @Sonuc;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK TRANSACTION;THROW;
 END CATCH;
END;
');

 EXEC(N'
CREATE OR ALTER PROCEDURE dbo.RezervasyonIptalEt @IslemId uniqueidentifier
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @@TRANCOUNT<>0 THROW 51103,N''İç içe işlem kabul edilmez.'',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @Durum tinyint;
  SELECT @Durum=Durum FROM dbo.Rezervasyonlar WITH(UPDLOCK,HOLDLOCK) WHERE IslemId=@IslemId;
  IF @Durum IS NULL THROW 51101,N''İşlem bulunamadı.'',1;
  IF @Durum=1 THROW 51102,N''Onaylı işlem iptal edilemez.'',1;
  UPDATE dbo.Rezervasyonlar SET Durum=2 WHERE IslemId=@IslemId;
  COMMIT TRANSACTION;
  SELECT IslemId,DosyaAdi,Durum,Tarih FROM dbo.Rezervasyonlar WHERE IslemId=@IslemId;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK TRANSACTION;THROW;
 END CATCH;
END;
');

 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;THROW;
END CATCH;
SELECT DB_NAME() AS Veritabani,N'Rezervasyon kurulumu tamamlandı.' AS Sonuc;
