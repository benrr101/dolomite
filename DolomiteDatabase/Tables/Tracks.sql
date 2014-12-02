CREATE TABLE [dbo].[Tracks]
(
	[Id]					BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1),
	[GuidId]				UNIQUEIDENTIFIER NOT NULL,
	[Owner]					INT NOT NULL,
	[DateAdded]				DATETIME NOT NULL DEFAULT GETUTCDATE(),
	[Hash]					NCHAR(40) NULL, 
    [Album]					INT NULL,
	[Art]					BIGINT NULL,
	[OriginalBitrate]		INT NULL,
	[OriginalSampling]		INT NULL,
	[OriginalMimetype]		NVARCHAR(30) NULL,
	[OriginalExtension]		NVARCHAR(10) NULL,
    [HasBeenOnboarded]		BIT NOT NULL DEFAULT 0, 
    [Locked]				BIT NOT NULL DEFAULT 0,
    [TrackInTempStorage]	BIT NOT NULL DEFAULT 0, 
    [ArtChange]				BIT NOT NULL DEFAULT 0
)
