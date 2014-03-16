CREATE PROCEDURE [dbo].[ResetOnboardingStatus]
	@guid UNIQUEIDENTIFIER
AS

	SET XACT_ABORT ON
	BEGIN TRANSACTION
		
	-- Remove the onboarded flag, and lock it at the same time
	UPDATE Tracks
		SET HasBeenOnboarded = ((0)), Locked = ((1))
		WHERE Id = @guid;

	-- Remove all quality records
	DELETE FROM AvailableQualities
		WHERE Track = @guid;

	-- Remove all metadata records
	DELETE FROM Metadata
		WHERE Track = @guid;

	-- Undo the lock
	UPDATE Tracks
		SET Locked = ((0))

	COMMIT TRANSACTION
RETURN
