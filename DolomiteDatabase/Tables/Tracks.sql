CREATE TABLE [dbo].[Tracks]
(
	[Id]	UNIQUEIDENTIFIER NOT NULL,
	[Owner] INT NULL,
	[Hash]	NCHAR(32) NULL, 
    [Album] INT NULL, 
    CONSTRAINT [PK_Tracks] PRIMARY KEY ([Id]) 
)
