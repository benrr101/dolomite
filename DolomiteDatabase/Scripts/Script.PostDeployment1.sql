/*
Post-Deployment Script
*/

-- Add some supported qualities to the database
INSERT INTO Qualities (Name) VALUES ('original');

-- Add some supported metadata fields to the database
INSERT INTO MetadataFields (TagName, DisplayName) 
VALUES 
	('Title', 'Title'), 
	('Performer', 'Artist'), 
	('AlbumArtist', 'Album Artist'),
	('Composer', 'Composer'),
	('Album', 'Album'),
	('Genre', 'Genre'),
	('Year', 'Year'),
	('Track', 'Track Number'),
	('TrackCount', 'Track Count'),
	('Disc', 'Disc Number'),
	('Lyrics', 'Lyrics'),
	('BeatsPerMinute', 'BPM'),
	('Conductor', 'Conductor'),
	('Copyright', 'Copyright');
