CREATE TABLE [dbo].[PlaylistTracks]
(
	[Id] INT NOT NULL PRIMARY KEY IDENTITY(1,1),
	[Playlist] INT NOT NULL,
	[Track] INT NOT NULL,
	[Order] INT NULL,
)
