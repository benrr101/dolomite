-- Add the statuses to the statuses table
MERGE INTO Statuses AS [target]
USING ( VALUES
	(1, 'Initial'),
	(2, 'Onboarding'),
	(3, 'Ready'),
	(4, 'MetadataUpdate'),
	(5, 'Error')
) AS [source] ([Id], [Status])
ON [source].[Id] = [target].[Id]
WHEN MATCHED THEN
	UPDATE SET [target].[Status] = [source].[Status]
WHEN NOT MATCHED BY TARGET THEN
	INSERT ([Id], [Status])
	VALUES ([Id], [Status])
WHEN NOT MATCHED BY SOURCE THEN
	DELETE;

GO