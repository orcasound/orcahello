namespace AIForOrcas.Server.Helpers;

public static class DetectionProcessors
{
	public static (int ReviewedCount, int UnreviewedCount) GetReviewed(List<Detection> list)
	{
		var status = list.GroupBy(n => n.Reviewed);

		var reviewed = status.Where(x => x.Key == true)
			.Select(x => x.Count()).FirstOrDefault();

		var unreviewed = status.Where(x => x.Key == false)
			.Select(x => x.Count()).FirstOrDefault();

		return (reviewed, unreviewed);
	}

	public static (int ConfirmedCount, int FalseCount, int UnknownCount) GetResults(List<Detection> list)
	{
		// grab results metrics
		var results = list.GroupBy(n => n.Found);

		var confirmed = results.Where(x => x.Key == "yes")
			.Select(x => x.Count()).FirstOrDefault();

		var unconfirmed = results.Where(x => x.Key == "no")
			.Select(x => x.Count()).FirstOrDefault();

		var unknown = results.Where(x => x.Key == "don't know")
			.Select(x => x.Count()).FirstOrDefault();

		return (confirmed, unconfirmed, unknown);
	}

	public static List<MetricsComment> GetComments(List<Detection> list, string status)
	{
		var results = new List<MetricsComment>();

		list
			.Where(x => x.Found == status && !string.IsNullOrWhiteSpace(x.Comments))
			.ToList().ForEach(x =>
			{
				results.Add(new MetricsComment()
				{
					Comment = x.Comments,
					Moderator = x.Moderator,
					Timestamp = x.Moderated,
					Id = x.Id
				});
			});

		return results.OrderByDescending(x => x.Timestamp).ToList();
	}

	public static List<MetricsTag> GetTags(List<Detection> list)
	{
		var results = new List<MetricsTag>();

		list
			.Where(x => !string.IsNullOrWhiteSpace(x.Tags))
			.ToList().ForEach(y =>
			{
				var id = y.Id;
				y.Tags.Split(";")
				.ToList().ForEach(z =>
				{
					var tag = results.Where(t => t.Tag == z.ToUpper()).FirstOrDefault();
					if (tag != null)
					{
						tag.Ids.Add(id);
					}
					else
					{
						var newTag = new MetricsTag()
						{
							Tag = z.ToUpper()
						};
						newTag.Ids.Add(id);
						results.Add(newTag);
					}
				});
			});

		return results.OrderBy(x => x.Tag).ToList();
	}

	public static Detection ToDetection(Metadata metadata)
	{
		var detection = new Detection()
		{
			Id = string.IsNullOrEmpty(metadata.id) ? Guid.NewGuid().ToString() : metadata.id,
			SpectrogramUri = string.IsNullOrWhiteSpace(metadata.imageUri) ? string.Empty : metadata.imageUri,
			AudioUri = string.IsNullOrWhiteSpace(metadata.audioUri) ? string.Empty : metadata.audioUri,
			Reviewed = metadata.reviewed,
			Confidence = metadata.whaleFoundConfidence,
			Found = string.IsNullOrWhiteSpace(metadata.SRKWFound) ? "No" : metadata.SRKWFound,
			Timestamp = metadata.timestamp,
			Comments = metadata.comments,
			Tags = metadata.tags,
			GlobalPredictionLabel = metadata.globalPredictionLabel ?? string.Empty,
			Moderated = string.IsNullOrWhiteSpace(metadata.dateModerated) ? DateTime.MinValue : DateTime.Parse(metadata.dateModerated),
			Moderator = metadata.moderator,
			Location = new DTO.API.Location()
			{
				Name = metadata.location.name,
				Longitude = metadata.location.longitude,
				Latitude = metadata.location.latitude
			}
		};

		if (metadata.predictions?.Count > 0)
		{
			metadata.predictions.ForEach(x =>
			{
				detection.Annotations.Add(new Annotation()
				{
					Id = x.id,
					Confidence = x.confidence,
					StartTime = x.startTime,
					EndTime = x.startTime + x.duration,
					Label = x.label ?? string.Empty,
				});
			});
		}

		return detection;
	}
}
