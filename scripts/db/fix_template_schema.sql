-- Drop existing tables with incorrect schema
IF OBJECT_ID(N'[dbo].[FieldMapping]', N'U') IS NOT NULL
    DROP TABLE [dbo].[FieldMapping];

IF OBJECT_ID(N'[dbo].[Templates]', N'U') IS NOT NULL
    DROP TABLE [dbo].[Templates];

-- Create Templates table with correct schema (TemplateId as nvarchar instead of uniqueidentifier)
CREATE TABLE [dbo].[Templates](
    [TemplateId] [nvarchar](450) NOT NULL,
    [TemplateType] [nvarchar](100) NOT NULL,
    [Version] [nvarchar](50) NOT NULL,
    [Name] [nvarchar](200) NOT NULL,
    [Description] [nvarchar](max) NULL,
    [EffectiveDate] [datetime2](7) NOT NULL,
    [ExpirationDate] [datetime2](7) NULL,
    [IsActive] [bit] NOT NULL,
    [XmlNamespace] [nvarchar](500) NULL,
    [RootElement] [nvarchar](200) NULL,
    [CreatedAt] [datetime2](7) NOT NULL,
    [CreatedBy] [nvarchar](200) NOT NULL,
    [ModifiedAt] [datetime2](7) NULL,
    [ModifiedBy] [nvarchar](200) NULL,
    CONSTRAINT [PK_Templates] PRIMARY KEY CLUSTERED ([TemplateId] ASC)
);

-- Create FieldMapping table with correct foreign key to Templates.TemplateId (nvarchar)
CREATE TABLE [dbo].[FieldMapping](
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [TemplateDefinitionTemplateId] [nvarchar](450) NOT NULL,
    [MappingId] [nvarchar](100) NULL,
    [SourceFieldPath] [nvarchar](500) NOT NULL,
    [TargetField] [nvarchar](200) NOT NULL,
    [DataType] [nvarchar](50) NOT NULL,
    [IsRequired] [bit] NOT NULL,
    [IsNullable] [bit] NOT NULL,
    [DefaultValue] [nvarchar](max) NULL,
    [Format] [nvarchar](100) NULL,
    [TransformExpression] [nvarchar](max) NULL,
    [DisplayName] [nvarchar](200) NULL,
    [DisplayOrder] [int] NOT NULL,
    [MaxLength] [int] NULL,
    CONSTRAINT [PK_FieldMapping] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_FieldMapping_Templates] FOREIGN KEY ([TemplateDefinitionTemplateId])
        REFERENCES [dbo].[Templates] ([TemplateId]) ON DELETE CASCADE
);

-- Create index for performance
CREATE NONCLUSTERED INDEX [IX_FieldMapping_TemplateDefinitionTemplateId]
    ON [dbo].[FieldMapping]([TemplateDefinitionTemplateId] ASC);

PRINT 'Templates and FieldMapping tables recreated successfully with correct schema';
