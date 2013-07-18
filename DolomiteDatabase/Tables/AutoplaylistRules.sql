CREATE TABLE [dbo].[AutoplaylistRules]
(
	[Id]			INT NOT NULL PRIMARY KEY,
	[Autoplaylist]	INT NOT NULL,
	[Rule]			INT NOT NULL,
	[MetadataField]	INT NOT NULL,
	[Value]			NVARCHAR(100) NOT NULL
)
