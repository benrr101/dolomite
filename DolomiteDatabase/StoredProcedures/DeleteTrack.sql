CREATE PROCEDURE [dbo].[DeleteTrack]
	@trackGuid UNIQUEIDENTIFIER
AS
BEGIN TRANSACTION
	-- Find the ID of the track to be deleted
	DECLARE @trackId BIGINT = (
		SELECT [Id] 
		FROM [Tracks] 
		WHERE [GuidId] = @trackGuid
	)

	-- Make sure there is only one track selected
	IF COUNT(@trackId) <> 1
	BEGIN
		RETURN 0
	END

	-- Delete any references to the track in playlists
	DELETE FROM [PlaylistTracks] WHERE [Track] = @trackId

	-- Delete the track's metadata
	DELETE FROM [Metadata] WHERE [Track] = @trackId

	-- Delete all records of the track in the quality table
	DELETE FROM [AvailableQualities] WHERE [Track] = @trackId

	-- Delete the track itself
	DELETE FROM [Tracks] WHERE [Id] = @trackId

COMMIT
RETURN 1
