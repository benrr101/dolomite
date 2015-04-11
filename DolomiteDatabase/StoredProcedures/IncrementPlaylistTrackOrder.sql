CREATE PROCEDURE [dbo].[IncrementPlaylistTrackOrder]
	@playlist BIGINT,
	@position INT
AS
	UPDATE [PlaylistTracks] 
		SET [Order] = [Order] + 1
		WHERE 
			[Order] >= @position
			AND [Playlist] = @playlist

RETURN 0
