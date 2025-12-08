IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251208013509_InitialCreate'
)
BEGIN
    CREATE TABLE [Templates] (
        [TemplateId] nvarchar(450) NOT NULL,
        [TemplateType] nvarchar(50) NOT NULL,
        [Version] nvarchar(20) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [EffectiveDate] datetime2 NOT NULL,
        [ExpirationDate] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [XmlNamespace] nvarchar(max) NULL,
        [RootElement] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_Templates] PRIMARY KEY ([TemplateId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251208013509_InitialCreate'
)
BEGIN
    CREATE TABLE [FieldMapping] (
        [TemplateDefinitionTemplateId] nvarchar(450) NOT NULL,
        [Id] int NOT NULL IDENTITY,
        [MappingId] nvarchar(max) NOT NULL,
        [SourceFieldPath] nvarchar(max) NOT NULL,
        [TargetField] nvarchar(max) NOT NULL,
        [DisplayName] nvarchar(max) NULL,
        [IsRequired] bit NOT NULL,
        [DataType] nvarchar(max) NOT NULL,
        [Format] nvarchar(max) NULL,
        [DefaultValue] nvarchar(max) NULL,
        [MaxLength] int NULL,
        [DisplayOrder] int NOT NULL,
        [IsNullable] bit NOT NULL,
        [TransformExpression] nvarchar(max) NULL,
        CONSTRAINT [PK_FieldMapping] PRIMARY KEY ([TemplateDefinitionTemplateId], [Id]),
        CONSTRAINT [FK_FieldMapping_Templates_TemplateDefinitionTemplateId] FOREIGN KEY ([TemplateDefinitionTemplateId]) REFERENCES [Templates] ([TemplateId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251208013509_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Templates_TemplateType_IsActive_EffectiveDate] ON [Templates] ([TemplateType], [IsActive], [EffectiveDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251208013509_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Templates_TemplateType_Version] ON [Templates] ([TemplateType], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251208013509_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251208013509_InitialCreate', N'10.0.0');
END;

COMMIT;
GO

