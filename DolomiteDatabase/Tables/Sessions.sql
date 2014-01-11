CREATE TABLE [dbo].[Sessions]
(
	[Id]				INT NOT NULL PRIMARY KEY IDENTITY(1,1),
	[Token]				NCHAR(64) NOT NULL,
	[User]				INT NOT NULL,
	[InitialIP]			NVARCHAR(15) NOT NULL,
	[ApiKey]			INT NOT NULL,
	[InitializedTime]	DATETIME NOT NULL DEFAULT GETDATE(),
	[IdleTimeout]		DATETIME NOT NULL,
	[AbsoluteTimeout]	DATETIME NOT NULL
)
