-- Add the supported metadata fields to the database
MERGE INTO MetadataFields AS [target]
USING ( VALUES
	-- File supported metadata fields
	(1,		'Artist',		'Artist',		'string',	((1)), ((1))), 
	(2,		'AlbumArtist',	'Album Artist', 'string',	((1)), ((1))),
	(3,		'Album',		'Album',		'string',	((1)), ((1))),
	(4,		'Composer',		'Composer',		'string',	((1)), ((1))),
	(5,		'Performer',	'Performer',	'string',	((1)), ((1))),
	(6,		'Date',			'Date',			'numeric',	((0)), ((1))),
	(7,		'Genre',		'Genre',		'string',	((1)), ((1))),
	(8,		'Title',		'Title',		'string',	((1)), ((1))), 
	(9,		'DiscNumber',	'Disc Number',	'numeric',	((0)), ((1))),
	(10,	'TotalDiscs',	'Total Discs',	'numeric',	((0)), ((1))),
	(11,	'TrackNumber',	'Track Number', 'numeric',	((0)), ((1))),
	(12,	'TotalTracks',	'Total Tracks', 'numeric',	((0)), ((1))),
	(13,	'Copyright',	'Copyright',	'string',	((1)), ((1))),
	(14,	'Comment',		'Comment',		'string',	((1)), ((1))),

	(15,	'DurationMilli','Duration',		'numeric',	((0)), ((0))),
	
	-- Dolomite-specific metadata fields
	(16,	'Dol:DateAdded',		'Date Added',		'date',		((0)), ((0))),
	(17,	'Dol:LastPlayed',		'Last Played',		'date',		((0)), ((0))),
	(18,	'Dol:PlayCount',		'Play Count',		'numeric',	((0)), ((0))),
	(19,	'Dol:SkipCount',		'Skip Count',		'numeric',	((0)), ((0))),
	(20,	'Dol:TrackListing',		'TrackListing',		'string',	((1)), ((0))),
	(21,	'Dol:OriginalBitrate',	'Original Bitrate',	'numeric',	((0)), ((0))),
	(22,	'Dol:OriginalCodec',	'Original Codec',	'string',	((0)), ((0)))
	) AS [source] ([Id], [TagName], [DisplayName], [Type], [Searchable], [FileSupported])
ON [source].[id] = [target].[id]
WHEN MATCHED THEN
	UPDATE SET 
		[target].[TagName] = [source].[TagName],
		[target].[DisplayName] = [source].[DisplayName],
		[target].[Type] = [source].[Type],
		[target].[Searchable] = [source].[Searchable],
		[target].[FileSupported] = [source].[FileSupported]
WHEN NOT MATCHED BY TARGET THEN
	INSERT ([Id], [TagName], [DisplayName], [Type], [Searchable], [FileSupported])
	VALUES ([Id], [TagName], [DisplayName], [Type], [Searchable], [FileSupported])
WHEN NOT MATCHED BY SOURCE THEN
	DELETE;

GO