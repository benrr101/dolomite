CREATE TABLE [dbo].[MetadataFields]
(
	[Id]			INT NOT NULL PRIMARY KEY,
	[TagName]		NVARCHAR(20) NOT NULL,
	[DisplayName]	NVARCHAR(20) NOT NULL, 
    [Type]			NVARCHAR(20) NOT NULL,
	[Searchable]	BIT NOT NULL DEFAULT ((1)),
	[FileSupported]	BIT NOT NULL DEFAULT ((0)), 
    [TagLibArray]	BIT NOT NULL DEFAULT ((0))
)
