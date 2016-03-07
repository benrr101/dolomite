CREATE PROCEDURE [dbo].[ReleaseAndCompleteOnboardingItem]
	@workItem BIGINT
AS
	-- Unlock the track and mark it's status as Ready (3)
	UPDATE Tracks
	  SET Locked = ((0)), [Status] = 3
	  WHERE Id = @workItem;
RETURN
