CREATE PROCEDURE [dbo].[ReleaseAndCompleteArtChange]
	@workItem UNIQUEIDENTIFIER
AS
	UPDATE Tracks
	  SET Locked = ((0)), ArtChange = ((0))
	  WHERE Id = @workItem;
RETURN