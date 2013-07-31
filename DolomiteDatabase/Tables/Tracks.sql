CREATE TABLE [dbo].[Tracks]
(
	[Id]	UNIQUEIDENTIFIER NOT NULL,
	[Owner] INT NULL,
	[Hash]	NCHAR(32) NULL, 
    [Album] INT NULL, 
    [HasBeenOnboarded] BIT NOT NULL DEFAULT 0, 
    [Locked] BIT NOT NULL DEFAULT 0, 
    CONSTRAINT [PK_Tracks] PRIMARY KEY ([Id]) 
)
