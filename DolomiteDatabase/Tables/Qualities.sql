CREATE TABLE [dbo].[Qualities]
(
	[Id]		INT NOT NULL PRIMARY KEY IDENTITY(1,1), 
    [Name]		NVARCHAR(20) NOT NULL, 
    [Codec]		NVARCHAR(50) NULL, 
    [Bitrate]	INT NULL, 
	[Extension]	NCHAR(3) NULL,
	[Mimetype]  NVARCHAR(30) NULL,
    [Directory] NVARCHAR(50) NOT NULL
)
