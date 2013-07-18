CREATE TABLE [dbo].[Qualities]
(
	[Id]		INT NOT NULL PRIMARY KEY, 
    [Name]		NVARCHAR(20) NOT NULL, 
    [Codec]		NVARCHAR(10) NOT NULL, 
    [Bitrate]	NCHAR(10) NOT NULL
)
