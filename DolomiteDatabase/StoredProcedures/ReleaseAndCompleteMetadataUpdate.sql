CREATE PROCEDURE [dbo].[ReleaseAndCompleteMetadataUpdate]
	@workItem UNIQUEIDENTIFIER
AS
	UPDATE Metadata
		SET WriteOut = ((0))
		WHERE Track = @workItem;

	UPDATE Tracks
	  SET Locked = ((0))
	  WHERE Id = @workItem;
RETURN
