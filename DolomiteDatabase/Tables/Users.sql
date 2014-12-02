﻿CREATE TABLE [dbo].[Users]
(
	[Id]			INT NOT NULL PRIMARY KEY IDENTITY(1,1), 
    [Username]		NVARCHAR(50) NOT NULL,
	[PasswordHash]	NCHAR(64) COLLATE SQL_Latin1_General_CP1_CS_AS NOT NULL,
	[PasswordReset] BIT NOT NULL DEFAULT ((0)),
	[Email]			NVARCHAR(255) NOT NULL
)
