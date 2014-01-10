CREATE TABLE [dbo].[Sessions]
(
	[Id]				INT NOT NULL PRIMARY KEY IDENTITY(1,1),
	[Token]				NCHAR(60) NOT NULL,
	[User]				INT NOT NULL,
	[ApiKey]			INT NOT NULL,
	[IdleTimeout]		DATETIME NOT NULL,
	[AbsoluteTimeout]	DATETIME NOT NULL
)
