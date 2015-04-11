CREATE PROCEDURE [dbo].[SetTrackHash]
	@trackId	BIGINT,
	@hash		text
AS
	UPDATE Tracks 
		SET [Hash] = @hash 
		WHERE [Id] = @trackId;
RETURN;
