CREATE TABLE [dbo].[Qualities]
(
	[Id]			INT NOT NULL PRIMARY KEY, 
    [Name]			NVARCHAR(20) NOT NULL, 
    [Codec]			NVARCHAR(50) NULL, 
    [Bitrate]		INT NULL, 
	[Extension]		NCHAR(3) NULL,
	[FfmpegArgs]	NVARCHAR(140) NULL,
	[Mimetype]		NVARCHAR(30) NULL,
    [Directory]		NVARCHAR(50) NOT NULL
)
