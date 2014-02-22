CREATE PROCEDURE [dbo].[GetAndLockTopArtItem]
AS
	DECLARE @workItem UNIQUEIDENTIFIER;

	-- Grab the foremost work item
	SELECT @workItem = (SELECT TOP 1 Id 
						FROM Tracks
						WHERE ArtChange = 1
							AND Locked = 0);
	
	-- Lock it up
	UPDATE Tracks SET Locked = 1 WHERE Id = @workItem;

	SELECT @workItem AS 'WorkItem'

RETURN;
