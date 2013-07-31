CREATE PROCEDURE [dbo].[SetTrackHash]
	@trackId	uniqueidentifier,
	@hash		text
AS
	UPDATE Tracks 
		SET [Hash] = @hash 
		WHERE [Id] = @trackId;
RETURN;
