CREATE TABLE [dbo].[Qualities]
(
	[Id]		INT NOT NULL PRIMARY KEY IDENTITY(1,1), 
    [Name]		NVARCHAR(20) NOT NULL, 
    [Codec]		NVARCHAR(10) NULL, 
    [Bitrate]	NCHAR(10) NULL
)
