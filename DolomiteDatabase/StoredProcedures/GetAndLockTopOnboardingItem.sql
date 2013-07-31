CREATE PROCEDURE [dbo].[GetAndLockTopOnboardingItem]
AS
	DECLARE @workItem UNIQUEIDENTIFIER;

	-- Grab the foremost work item
	SELECT @workItem = (SELECT TOP 1 id 
						FROM Tracks
						WHERE HasBeenOnboarded = 0
							AND Locked = 0);
	
	-- Lock it up
	UPDATE Tracks SET Locked = 1 WHERE Id = @workItem;

	SELECT @workItem AS 'WorkItem'

RETURN;
