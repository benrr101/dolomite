﻿CREATE TABLE [dbo].[Autoplaylists]
(
	[Id]	INT NOT NULL PRIMARY KEY,
	[Owner]	INT NOT NULL,
	[Name]	NVARCHAR(100) NOT NULL
)
