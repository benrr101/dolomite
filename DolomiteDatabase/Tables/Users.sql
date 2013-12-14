CREATE TABLE [dbo].[Users]
(
	[Id]			INT NOT NULL PRIMARY KEY, 
    [Username]		NVARCHAR(50) NOT NULL,
	[PasswordHash]	NCHAR(64) NOT NULL,
	[PasswordReset] BIT NOT NULL DEFAULT ((0)),
	[Email]			NVARCHAR(255) NOT NULL
)
