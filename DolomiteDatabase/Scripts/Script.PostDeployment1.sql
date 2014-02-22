/*
Post-Deployment Script
*/

-- Add some supported metadata fields to the database
INSERT INTO MetadataFields ([TagName], [DisplayName], [Type], [Searchable], [FileSupported], [TagLibArray]) 
VALUES 
	('Title', 'Title', 'string', ((1)), ((1)), ((0))), 
	('Artist', 'Artist', 'string', ((1)), ((1)), ((1))), 
	('AlbumArtist', 'Album Artist', 'string', ((1)), ((1)), ((1))),
	('Composer', 'Composer', 'string', ((1)), ((1)), ((1))),
	('Album', 'Album', 'string', ((1)), ((1)), ((0))),
	('Genre', 'Genre', 'string', ((1)), ((1)), ((1))),
	('Year', 'Year', 'numeric', ((0)), ((1)), ((0))),
	('Track', 'Track Number', 'numeric', ((1)), ((1)), ((0))),
	('TrackCount', 'Track Count', 'numeric', ((0)), ((1)), ((0))),
	('Disc', 'Disc Number', 'numeric', ((0)), ((1)), ((0))),
	('Lyrics', 'Lyrics', 'string', ((0)), ((1)), ((0))),
	('BeatsPerMinute', 'BPM', 'numeric', ((0)), ((1)), ((0))),
	('Conductor', 'Conductor', 'string', ((1)), ((1)), ((0))),
	('Copyright', 'Copyright', 'string', ((1)), ((1)), ((0))),
	('Comment', 'Comment', 'string', ((1)), ((1)), ((0))),
	('DiscCount', 'Disc Count', 'numeric', ((0)), ((1)), ((0))),
	('DateAdded', 'Date Added', 'date', ((0)), ((0)), ((0))),
	('PlayCount', 'Play Count', 'numeric', ((0)), ((0)), ((0))),
	('LastPlayed', 'Last Played', 'date', ((0)), ((0)), ((0))),
	('Duration', 'Duration', 'numeric', ((0)), ((0)), ((0))),
	('TrackListing', 'Track Listing', 'string', ((1)), ((0)), ((0))),
	('OriginalBitrate', 'Original Bitrate', 'numeric', ((0)), ((0)), ((0))),
	('OriginalFormat', 'Original Format', 'numeric', ((0)), ((0)), ((0)))
;

-- Add some supported audio qualities for transcoding
INSERT INTO Qualities (Name, Codec, Bitrate, Extension, Directory, Mimetype) 
VALUES
	--('AAC 64Kbps', 'libfdk_aac -aprofile aac_he', 64, 'aac', 'aac_64'),
	('MP3 128Kbps', 'mp3', 128, 'mp3', 'mp3_128', 'audio/mpeg'),
	-- ('MP3 160Kbps', 'mp3', 160, 'mp3', 'mp3_160', 'audio/mpeg'),
	('MP3 192Kbps', 'mp3', 192, 'mp3', 'mp3_192', 'audio/mpeg'),
	-- ('MP3 256Kbps', 'mp3', 256, 'mp3', 'mp3_256', 'audio/mpeg'),
	('MP3 320Kbps', 'mp3', 320, 'mp3', 'mp3_320', 'audio/mpeg'),
	('Original', NULL, NULL, NULL, 'original', NULL);
GO

-- Add supported metadata rules/comparisons
INSERT INTO Rules ([Name], [DisplayName], [Type])
VALUES
	('contains', 'Contains', 'string'),
	('sequals', 'Equals', 'string'),
	('snotequal', 'Not Equal To', 'string'),
	('startswith', 'Starts With', 'string'),
	('endswith', 'Ends With', 'string'),
	('notcontains', 'Does Not Contain', 'string'),
	('greaterthan', 'Greater Than', 'numeric'),
	('lessthan', 'Less Than', 'numeric'),
	('greaterthanequal', 'Greater Than Or Equal To', 'numeric'),
	('lessthanequal', 'Less Than Or Equal To', 'numeric'),
	('equal', 'Equals', 'numeric'),
	('notequal', 'Not Equal To', 'numeric'),
	('dequal', 'Equals', 'date'),
	('dnotequal', 'Not Equal To', 'date'),
	('isafter', 'Is After', 'date'),
	('isbefore', 'Is Before', 'date'),
	('inlastdays', 'In Last n Days', 'date'),
	('notinlastdays', 'Not In Last n Days', 'date');

-- Add important error messages
EXEC sp_addmessage 50010, 13, 
   N'The track that has been requested is currently locked.';
GO