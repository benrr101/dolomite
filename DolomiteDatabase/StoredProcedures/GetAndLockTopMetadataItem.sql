CREATE PROCEDURE [dbo].[GetAndLockTopMetadataItem]
AS
	DECLARE @workItem UNIQUEIDENTIFIER;

	-- Grab the foremost work item
	SELECT @workItem = (SELECT TOP 1 T.Id
						FROM Metadata AS M
						JOIN Tracks AS T ON M.Track = T.Id
						WHERE M.WriteOut = ((1)) AND T.Locked = ((0)));

	-- Lock the track
	UPDATE Tracks SET Locked = 1 WHERE Id = @workItem;

	SELECT @workItem AS 'WorkItem'
RETURN 0
