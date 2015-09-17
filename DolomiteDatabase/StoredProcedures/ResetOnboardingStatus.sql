CREATE PROCEDURE [dbo].[ResetOnboardingStatus]
	@trackId BIGINT
AS

	SET XACT_ABORT ON
	BEGIN TRANSACTION
		
	-- Remove the onboarded flag, and lock it at the same time
	-- Status = 1 sets the track to Initial
	UPDATE Tracks
		SET [Status] = 1, Locked = ((1))
		WHERE Id = @trackId;

	-- Remove all quality records
	DELETE FROM AvailableQualities
		WHERE Track = @trackId;

	-- Remove all metadata records
	DELETE FROM Metadata
		WHERE Track = @trackId;

	-- Undo the lock
	UPDATE Tracks
		SET Locked = ((0))

	COMMIT TRANSACTION
RETURN
