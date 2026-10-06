-- One-time baseline for a database that was created before EF Core migrations were used.
-- The live Azure SQL database was built by hand, so its tables exist but EF Core has no
-- __EFMigrationsHistory table. If that is the case, record InitialBaseline as already applied,
-- so the idempotent migration script skips it and only runs the migrations that come after it.
-- Safe to run on every deploy: it does nothing once the history table exists, and it does
-- nothing on an empty database (there the idempotent script creates everything).
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL AND OBJECT_ID(N'[dbo].[Users]') IS NOT NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003134346_InitialBaseline', N'8.0.30');

    PRINT 'Baseline recorded: existing tables marked as InitialBaseline.';
END
ELSE
    PRINT 'Baseline not needed.';
