/*
Post-Deployment Script
*/

-- Add some supported metadata fields to the database
INSERT INTO MetadataFields ([TagName], [DisplayName], [Type]) 
VALUES 
	('Title', 'Title', 'string', ((1))), 
	('Artist', 'Artist', 'string', ((1))), 
	('AlbumArtist', 'Album Artist', 'string', ((1))),
	('Composer', 'Composer', 'string', ((1))),
	('Album', 'Album', 'string', ((1))),
	('Genre', 'Genre', 'string', ((1))),
	('Year', 'Year', 'numeric', ((1))),
	('Track', 'Track Number', 'numeric', ((1))),
	('TrackCount', 'Track Count', 'numeric', ((1))),
	('Disc', 'Disc Number', 'numeric', ((1))),
	('Lyrics', 'Lyrics', 'string', ((1))),
	('BeatsPerMinute', 'BPM', 'numeric', ((1))),
	('Conductor', 'Conductor', 'string', ((1))),
	('Copyright', 'Copyright', 'string', ((1))),
	('Comment', 'Comment', 'string', ((1))),
	('DiscCount', 'Disc Count', 'numeric', ((1))),
	('DateAdded', 'Date Added', 'date', ((0))),
	('PlayCount', 'Play Count', 'numeric', ((0))),
	('LastPlayed', 'Last Played', 'date', ((0))),
	('Duration', 'Duration', 'numeric', ((0))),
	('TrackListing', 'Track Listing', 'string', ((0))
);

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