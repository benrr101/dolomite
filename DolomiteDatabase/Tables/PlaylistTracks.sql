CREATE TABLE [dbo].[PlaylistTracks]
(
	[Id]		BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1),
	[Playlist]	BIGINT NOT NULL,
	[Track]		BIGINT NOT NULL,
	[Order]		INT NULL,
)
