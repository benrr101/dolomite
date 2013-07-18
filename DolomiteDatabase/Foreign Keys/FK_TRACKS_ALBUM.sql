ALTER TABLE [dbo].[Tracks]
	ADD CONSTRAINT [Album]
	FOREIGN KEY (Album)
	REFERENCES [Albums] (Id)
