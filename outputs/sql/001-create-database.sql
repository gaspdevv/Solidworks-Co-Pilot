-- Run in SSMS against the intended SQL Server instance.
-- Re-running this script preserves an existing database.
USE [master];
GO

IF DB_ID(N'SolidWorksAutomation') IS NULL
BEGIN
    CREATE DATABASE [SolidWorksAutomation];
END;
GO

SELECT name AS DatabaseName, state_desc AS DatabaseState
FROM sys.databases
WHERE name = N'SolidWorksAutomation';
GO
