-- Add the supported metadata fields to the database
MERGE INTO MetadataFields AS [target]
USING ( VALUES
	(1, 'Title', 'Title', 'string', ((1)), ((1)), ((0))), 
	(2, 'Artist', 'Artist', 'string', ((1)), ((1)), ((1))), 
	(3, 'AlbumArtist', 'Album Artist', 'string', ((1)), ((1)), ((1))),
	(4, 'Composer', 'Composer', 'string', ((1)), ((1)), ((1))),
	(5, 'Album', 'Album', 'string', ((1)), ((1)), ((0))),
	(6, 'Genre', 'Genre', 'string', ((1)), ((1)), ((1))),
	(7, 'Year', 'Year', 'numeric', ((0)), ((1)), ((0))),
	(8, 'Track', 'Track Number', 'numeric', ((1)), ((1)), ((0))),
	(9, 'TrackCount', 'Track Count', 'numeric', ((0)), ((1)), ((0))),
	(10, 'Disc', 'Disc Number', 'numeric', ((0)), ((1)), ((0))),
	(11, 'Lyrics', 'Lyrics', 'string', ((0)), ((1)), ((0))),
	(12, 'BeatsPerMinute', 'BPM', 'numeric', ((0)), ((1)), ((0))),
	(13, 'Conductor', 'Conductor', 'string', ((1)), ((1)), ((0))),
	(14, 'Copyright', 'Copyright', 'string', ((1)), ((1)), ((0))),
	(15, 'Comment', 'Comment', 'string', ((1)), ((1)), ((0))),
	(16, 'DiscCount', 'Disc Count', 'numeric', ((0)), ((1)), ((0))),
	(17, 'DateAdded', 'Date Added', 'date', ((0)), ((0)), ((0))),
	(18, 'PlayCount', 'Play Count', 'numeric', ((0)), ((0)), ((0))),
	(19, 'LastPlayed', 'Last Played', 'date', ((0)), ((0)), ((0))),
	(20, 'Duration', 'Duration', 'numeric', ((0)), ((0)), ((0))),
	(21, 'TrackListing', 'Track Listing', 'string', ((1)), ((0)), ((0))),
	(22, 'OriginalBitrate', 'Original Bitrate', 'numeric', ((0)), ((0)), ((0))),
	(23, 'OriginalFormat', 'Original Format', 'numeric', ((0)), ((0)), ((0)))
	) AS [source] ([Id], [TagName], [DisplayName], [Type], [Searchable], [FileSupported], [TagLibArray])
ON [source].[id] = [target].[id]
WHEN MATCHED THEN
	UPDATE SET 
		[target].[TagName] = [source].[TagName],
		[target].[DisplayName] = [source].[DisplayName],
		[target].[Type] = [source].[Type],
		[target].[Searchable] = [source].[Searchable],
		[target].[FileSupported] = [source].[FileSupported],
		[target].[TagLibArray] = [source].[TagLibArray]
WHEN NOT MATCHED BY TARGET THEN
	INSERT ([Id], [TagName], [DisplayName], [Type], [Searchable], [FileSupported], [TagLibArray])
	VALUES ([Id], [TagName], [DisplayName], [Type], [Searchable], [FileSupported], [TagLibArray])
WHEN NOT MATCHED BY SOURCE THEN
	DELETE;