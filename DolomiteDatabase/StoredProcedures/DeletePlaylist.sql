CREATE PROCEDURE [dbo].[DeletePlaylist]
	@playlistGuid UNIQUEIDENTIFIER
AS
BEGIN TRANSACTION
	-- Find the ID of the playlist to be deleted
	DECLARE @playlistId BIGINT = (
		SELECT [Id] 
		FROM [Playlists] 
		WHERE [GuidId] = @playlistGuid
	)

	-- Make sure there is only one playlist selected
	IF COUNT(@playlistId) <> 1
	BEGIN
		RETURN 0
	END

	-- Delete all the tracks in the playlist
	DELETE FROM [PlaylistTracks] WHERE [Playlist] = @playlistId

	-- Delete the playlist, itself
	DELETE FROM [Playlists] WHERE [Id] = @playlistId

COMMIT
RETURN 1
