CREATE TABLE [dbo].[MetadataFields]
(
	[Id]			INT NOT NULL PRIMARY KEY IDENTITY(1,1),
	[TagName]		NVARCHAR(20) NOT NULL,
	[DisplayName]	NVARCHAR(20) NOT NULL, 
    [Type]			NVARCHAR(20) NOT NULL,
	[FileSupported]	BIT NOT NULL DEFAULT ((0)), 
    [TagLibArray]	BIT NOT NULL DEFAULT ((0))
)
