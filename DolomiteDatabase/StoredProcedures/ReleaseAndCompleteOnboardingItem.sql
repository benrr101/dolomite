CREATE PROCEDURE [dbo].[ReleaseAndCompleteOnboardingItem]
	@workItem UNIQUEIDENTIFIER
AS
	UPDATE Tracks
	  SET Locked = ((0)), HasBeenOnboarded = ((1))
	  WHERE Id = @workItem;
RETURN
