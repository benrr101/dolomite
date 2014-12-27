-- Add supported metadata rules/comparisons
MERGE INTO Rules AS [target]
USING (	VALUES
	(1, 'contains', 'Contains', 'string'),
	(2, 'sequals', 'Equals', 'string'),
	(3, 'snotequal', 'Not Equal To', 'string'),
	(4, 'startswith', 'Starts With', 'string'),
	(5, 'endswith', 'Ends With', 'string'),
	(6, 'notcontains', 'Does Not Contain', 'string'),
	(7, 'greaterthan', 'Greater Than', 'numeric'),
	(8, 'lessthan', 'Less Than', 'numeric'),
	(9, 'greaterthanequal', 'Greater Than Or Equal To', 'numeric'),
	(10, 'lessthanequal', 'Less Than Or Equal To', 'numeric'),
	(11, 'equal', 'Equals', 'numeric'),
	(12, 'notequal', 'Not Equal To', 'numeric'),
	(13, 'dequal', 'Equals', 'date'),
	(14, 'dnotequal', 'Not Equal To', 'date'),
	(15, 'isafter', 'Is After', 'date'),
	(16, 'isbefore', 'Is Before', 'date'),
	(17, 'inlastdays', 'In Last n Days', 'date'),
	(18, 'notinlastdays', 'Not In Last n Days', 'date')
	) AS [source] ([Id], [Name], [DisplayName], [Type])
ON [source].[Id] = [target].[Id]
WHEN MATCHED THEN
	UPDATE SET 
		[target].[Name] = [source].[Name],
		[target].[DisplayName] = [source].[DisplayName],
		[target].[Type] = [source].[Type]
WHEN NOT MATCHED BY TARGET THEN
	INSERT ([Id], [Name], [DisplayName], [Type])
	VALUES ([Id], [Name], [DisplayName], [Type])
WHEN NOT MATCHED BY SOURCE THEN
	DELETE;
