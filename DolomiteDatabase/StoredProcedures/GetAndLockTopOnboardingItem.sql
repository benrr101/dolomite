CREATE PROCEDURE [dbo].[GetAndLockTopOnboardingItem]
AS
	DECLARE @workItem BIGINT;

	-- Grab the foremost work item
	SELECT @workItem = (SELECT TOP 1 Id 
						FROM Tracks
						WHERE
							[Status] = 1		-- Initial status
							AND Locked = 0);
	
	-- Lock it up
	UPDATE Tracks 
		SET 
			Locked = 1,
			[Status] = 2	-- Onboarding status
		WHERE Id = @workItem;

	SELECT @workItem AS 'WorkItem'
RETURN;
