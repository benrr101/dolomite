CREATE PROCEDURE [dbo].[DeleteAutoplaylist]
	@autoplaylistGuid UNIQUEIDENTIFIER
AS
BEGIN TRANSACTION
	-- Find the ID of the track to be deleted
	DECLARE @autoplaylistId BIGINT = (
		SELECT [Id] 
		FROM [Autoplaylists] 
		WHERE [GuidId] = @autoplaylistGuid
	)

	-- Make sure there is only one track selected
	IF COUNT(@autoplaylistId) <> 1
	BEGIN
		RETURN 0
	END

	-- Delete all rules that belong to this autoplaylist
	DELETE FROM [AutoplaylistRules] WHERE [Autoplaylist] = @autoplaylistId;

	-- Delete the playlist itself
	DELETE FROM [Autoplaylists] WHERE [Id] = @autoplaylistId;

COMMIT
RETURN 1
