CREATE PROCEDURE [dbo].[ResetOnboardingStatus]
	@guid UNIQUEIDENTIFIER
AS

	BEGIN TRANSACTION

	IF (SELECT Locked FROM Tracks WHERE Id = @guid) = ((1))
	BEGIN
		ROLLBACK TRANSACTION
		RAISERROR (50010,-1,-1)
	END
	ELSE
	BEGIN
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
	END
RETURN
