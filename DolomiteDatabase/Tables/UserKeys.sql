﻿CREATE TABLE [dbo].[UserKeys]
(
	[Id]		UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
	[Email]		NVARCHAR(255) NULL DEFAULT NULL,
	[Claimed]	BIT NOT NULL DEFAULT ((0))
);
