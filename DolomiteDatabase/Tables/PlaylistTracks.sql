CREATE TABLE [dbo].[PlaylistTracks]
(
	[Id] INT NOT NULL PRIMARY KEY,
	[Playlist] INT NOT NULL,
	[Track] INT NOT NULL,
	[Order] INT NULL,
)
