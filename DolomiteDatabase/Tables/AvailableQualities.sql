CREATE TABLE [dbo].[AvailableQualities]
(
	[Id]		BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1), 
    [Track]		BIGINT NOT NULL, 
    [Quality]	INT NOT NULL 
)
