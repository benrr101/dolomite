CREATE PROCEDURE [dbo].[ReleaseAndCompleteArtChange]
	@workItem BIGINT
AS
	UPDATE Tracks
	  SET Locked = ((0)), ArtChange = ((0))
	  WHERE Id = @workItem;
RETURN