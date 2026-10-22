-- Ana veritabanina dokunmadan API integration testleri icin ayri bos veritabani.
USE [master];
GO
IF DB_ID(N'SolidWorksAutomation_ApiTest_20261008') IS NULL
    CREATE DATABASE [SolidWorksAutomation_ApiTest_20261008];
GO
-- Ardindan 002-documents-tablosu.sql ve 003-etiket-dagitim-motoru.sql
-- dosyalarinin USE satirlarini bu test veritabanina yonlendiren kopyalarini calistirin.
-- Var olan test kayitlari otomatik silinmez veya sayaclar sifirlanmaz.
