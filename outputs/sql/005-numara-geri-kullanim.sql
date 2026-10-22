-- 005: iptali doğrulanmış otomatik adları yeniden dağıt. API kapalıyken hedef DB'de çalıştır.
-- Kayıt/ID/sayaç sıfırlamaz; iptal GUID geçmişini korur. 004'ten sonra uygulanır.
SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON; SET ARITHABORT ON; SET NUMERIC_ROUNDABORT OFF; SET XACT_ABORT ON;
IF DB_ID()<=4 THROW 51110,N'Hedef uygulama veritabanını seçin.',1;
IF OBJECT_ID(N'dbo.Rezervasyonlar',N'U') IS NULL OR OBJECT_ID(N'dbo.Documents',N'U') IS NULL
 OR OBJECT_ID(N'dbo.ParcaSayaci',N'SO') IS NULL OR OBJECT_ID(N'dbo.MontajSayaci',N'SO') IS NULL
 THROW 51114,N'Önce 004 rezervasyon kurulumu gerekli.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @Kilit int,@Adet bigint;
 EXEC @Kilit=sys.sp_getapplock @Resource=N'Pronom-Sema-Gecisi',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
 IF @Kilit<0 THROW 51111,N'Kurulum kilidi alınamadı.',1;
 SELECT @Adet=COUNT_BIG(*) FROM dbo.Rezervasyonlar WITH(TABLOCKX,HOLDLOCK);
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Rezervasyonlar') AND name=N'UX_Rezervasyonlar_AktifDosyaAdi')
 CREATE UNIQUE INDEX UX_Rezervasyonlar_AktifDosyaAdi ON dbo.Rezervasyonlar(DosyaAdi) WHERE Durum<2;
 IF OBJECT_ID(N'dbo.UQ_Rezervasyonlar_DosyaAdi',N'UQ') IS NOT NULL
 ALTER TABLE dbo.Rezervasyonlar DROP CONSTRAINT UQ_Rezervasyonlar_DosyaAdi;
 EXEC(N'CREATE OR ALTER PROCEDURE dbo.DosyaAdiAyir @IslemId uniqueidentifier,@BelgeTuru nvarchar(10)
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;SET ARITHABORT ON;SET ANSI_WARNINGS ON;
 IF @@TRANCOUNT<>0 THROW 51103,N''İç içe işlem kabul edilmez.'',1;
 IF @IslemId IS NULL OR @IslemId=''00000000-0000-0000-0000-000000000000''
  OR @BelgeTuru IS NULL OR DATALENGTH(@BelgeTuru)<>2
  OR @BelgeTuru COLLATE Latin1_General_100_BIN2 NOT IN(N''P'',N''M'') THROW 51103,N''İşlem kimliği ve P/M türü gereklidir.'',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @Kilit int,@Kaynak nvarchar(255)=N''Pronom-Rezervasyon:''+CONVERT(nvarchar(36),@IslemId),@Havuz nvarchar(255)=N''Pronom-AdHavuzu:''+@BelgeTuru,@DosyaAdi nvarchar(32),@Numara bigint,@Rakam nvarchar(20);
  EXEC @Kilit=sys.sp_getapplock @Resource=@Kaynak,@LockMode=N''Exclusive'',@LockOwner=N''Transaction'',@LockTimeout=10000;
  IF @Kilit<0 THROW 51105,N''İşlem kilidi alınamadı.'',1;
  SELECT @DosyaAdi=DosyaAdi FROM dbo.Rezervasyonlar WHERE IslemId=@IslemId;
  IF @DosyaAdi IS NOT NULL
  BEGIN
   IF LEFT(@DosyaAdi,1)<>@BelgeTuru THROW 51104,N''Aynı işlem kimliği farklı tür için kullanılamaz.'',1;
  END
  ELSE
  BEGIN
   EXEC @Kilit=sys.sp_getapplock @Resource=@Havuz,@LockMode=N''Exclusive'',@LockOwner=N''Transaction'',@LockTimeout=10000;
   IF @Kilit<0 THROW 51105,N''Ad havuzu kilidi alınamadı.'',1;
   SELECT TOP(1) @DosyaAdi=r.DosyaAdi
   FROM dbo.Rezervasyonlar r
   WHERE r.Durum=2 AND LEFT(r.DosyaAdi,1)=@BelgeTuru
    AND TRY_CONVERT(bigint,SUBSTRING(r.DosyaAdi,2,LEN(r.DosyaAdi)-8))>0
    AND NOT EXISTS(SELECT 1 FROM dbo.Rezervasyonlar a WHERE a.DosyaAdi=r.DosyaAdi AND a.Durum<2)
    AND NOT EXISTS(SELECT 1 FROM dbo.Documents d WHERE d.DosyaAdi=r.DosyaAdi)
   ORDER BY TRY_CONVERT(bigint,SUBSTRING(r.DosyaAdi,2,LEN(r.DosyaAdi)-8)),r.Tarih,r.IslemId;
   IF @DosyaAdi IS NULL
   BEGIN
    IF @BelgeTuru=N''P'' SET @Numara=NEXT VALUE FOR dbo.ParcaSayaci;
    ELSE SET @Numara=NEXT VALUE FOR dbo.MontajSayaci;
    SET @Rakam=CONVERT(nvarchar(20),@Numara);
    SET @DosyaAdi=@BelgeTuru+REPLICATE(N''0'',CASE WHEN LEN(@Rakam)<5 THEN 5-LEN(@Rakam) ELSE 0 END)+@Rakam
     +CASE WHEN @BelgeTuru=N''P'' THEN N''.SLDPRT'' ELSE N''.SLDASM'' END;
   END;
   IF EXISTS(SELECT 1 FROM dbo.Documents WHERE DosyaAdi=@DosyaAdi) THROW 51104,N''Üretilen ad zaten kayıtlı; sayaç incelenmelidir.'',1;
   INSERT dbo.Rezervasyonlar(IslemId,DosyaAdi,Durum)VALUES(@IslemId,@DosyaAdi,0);
  END;
  COMMIT TRANSACTION;
  SELECT IslemId,DosyaAdi,Durum,Tarih FROM dbo.Rezervasyonlar WHERE IslemId=@IslemId;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK TRANSACTION;THROW;
 END CATCH;
END;');
 EXEC(N'CREATE OR ALTER PROCEDURE dbo.RezervasyonOnayla @IslemId uniqueidentifier
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;SET ARITHABORT ON;SET ANSI_WARNINGS ON;
 IF @@TRANCOUNT<>0 THROW 51103,N''İç içe işlem kabul edilmez.'',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @Kilit int,@Kaynak nvarchar(255)=N''Pronom-Rezervasyon:''+CONVERT(nvarchar(36),@IslemId),@Havuz nvarchar(255),@Durum tinyint,@DosyaAdi nvarchar(32);
  EXEC @Kilit=sys.sp_getapplock @Resource=@Kaynak,@LockMode=N''Exclusive'',@LockOwner=N''Transaction'',@LockTimeout=10000;
  IF @Kilit<0 THROW 51105,N''İşlem kilidi alınamadı.'',1;
  SELECT @Durum=Durum,@DosyaAdi=DosyaAdi FROM dbo.Rezervasyonlar WHERE IslemId=@IslemId;
  IF @Durum IS NULL THROW 51101,N''İşlem bulunamadı.'',1;
  IF @Durum=2 THROW 51102,N''İptal edilmiş işlem onaylanamaz.'',1;
  SET @Havuz=N''Pronom-AdHavuzu:''+LEFT(@DosyaAdi,1);
  EXEC @Kilit=sys.sp_getapplock @Resource=@Havuz,@LockMode=N''Exclusive'',@LockOwner=N''Transaction'',@LockTimeout=10000;
  IF @Kilit<0 THROW 51105,N''Ad havuzu kilidi alınamadı.'',1;
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
END;');
 EXEC(N'CREATE OR ALTER PROCEDURE dbo.RezervasyonIptalEt @IslemId uniqueidentifier
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;SET ARITHABORT ON;SET ANSI_WARNINGS ON;
 IF @@TRANCOUNT<>0 THROW 51103,N''İç içe işlem kabul edilmez.'',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @Kilit int,@Kaynak nvarchar(255)=N''Pronom-Rezervasyon:''+CONVERT(nvarchar(36),@IslemId),@Havuz nvarchar(255),@Durum tinyint,@DosyaAdi nvarchar(32);
  EXEC @Kilit=sys.sp_getapplock @Resource=@Kaynak,@LockMode=N''Exclusive'',@LockOwner=N''Transaction'',@LockTimeout=10000;
  IF @Kilit<0 THROW 51105,N''İşlem kilidi alınamadı.'',1;
  SELECT @Durum=Durum,@DosyaAdi=DosyaAdi FROM dbo.Rezervasyonlar WHERE IslemId=@IslemId;
  IF @Durum IS NULL THROW 51101,N''İşlem bulunamadı.'',1;
  IF @Durum=1 THROW 51102,N''Onaylı işlem iptal edilemez.'',1;
  SET @Havuz=N''Pronom-AdHavuzu:''+LEFT(@DosyaAdi,1);
  EXEC @Kilit=sys.sp_getapplock @Resource=@Havuz,@LockMode=N''Exclusive'',@LockOwner=N''Transaction'',@LockTimeout=10000;
  IF @Kilit<0 THROW 51105,N''Ad havuzu kilidi alınamadı.'',1;
  UPDATE dbo.Rezervasyonlar SET Durum=2 WHERE IslemId=@IslemId;
  COMMIT TRANSACTION;
  SELECT IslemId,DosyaAdi,Durum,Tarih FROM dbo.Rezervasyonlar WHERE IslemId=@IslemId;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK TRANSACTION;THROW;
 END CATCH;
END;');
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK TRANSACTION;THROW;
END CATCH;
SELECT DB_NAME() AS Veritabani,N'Numara geri kullanım kurulumu tamamlandı.' AS Sonuc;