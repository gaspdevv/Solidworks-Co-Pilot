-- 20 Ekim 2026: Bu mevcut veritabani Kullanim olarak benimsendi; ad ve sayaclar korunur.
-- Merkez SQL sunucusunda çalıştırılır. Yalnız SolidWorksAutomation_ApiTest_20261012_01 yedeklenir.
-- Ayrı yeni .bak oluşturur; eski yedeklerin üzerine yazmaz, kayıt/sayaç sıfırlamaz.
USE [master];
SET NOCOUNT ON;
IF DB_ID(N'SolidWorksAutomation_ApiTest_20261012_01') IS NULL THROW 51201,N'Kullanım veritabanı bulunamadı; test veritabanına geçilmedi.',1;
DECLARE @klasor nvarchar(4000)=CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath'));
IF NULLIF(@klasor,N'') IS NULL THROW 51202,N'SQL varsayılan yedek klasörü bulunamadı. SQL yedek klasörü ayarlanmalı.',1;
IF RIGHT(@klasor,1) NOT IN(N'\',N'/') SET @klasor=@klasor+N'\';
DECLARE @dosya nvarchar(4000)=@klasor+N'SolidWorksAutomation_ApiTest_20261012_01_'+REPLACE(CONVERT(nvarchar(36),NEWID()),N'-',N'')+N'.bak';
BACKUP DATABASE [SolidWorksAutomation_ApiTest_20261012_01] TO DISK=@dosya WITH COPY_ONLY,CHECKSUM,NOINIT;
RESTORE VERIFYONLY FROM DISK=@dosya WITH CHECKSUM;
SELECT @dosya AS DogrulananYedekDosyasi, SYSUTCDATETIME() AS KontrolZamaniUTC;
-- VERIFYONLY tam geri yükleme denemesi değildir. .bak ayrıca başka diske kopyalanmalı.
