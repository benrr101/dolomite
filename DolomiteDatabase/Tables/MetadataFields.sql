CREATE TABLE [dbo].[MetadataFields]
(
	[Id]			INT NOT NULL PRIMARY KEY,
	[Field]			NVARCHAR(20) NOT NULL, 
    [AllowedRules]	NVARCHAR(150) NULL
)
