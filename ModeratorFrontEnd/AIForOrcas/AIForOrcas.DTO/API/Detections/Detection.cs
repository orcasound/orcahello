using System;
using System.Collections.Generic;

namespace AIForOrcas.DTO.API
{
	/// <summary>
	/// A hydrophone sampling that might contain whale sounds.
	/// </summary>
	public class Detection
	{
		/// <summary>
		/// The detection's generated unique Id.
		/// </summary>
		/// <example>00000000-0000-0000-0000-000000000000</example>
		public string Id { get; set; }

		/// <summary>
		/// URI of the detection's audio file (.wav) in blob storage.
		/// </summary>
		/// <example>https://storagesite.blob.core.windows.net/audiowavs/audiofilename.wav</example>
		public string AudioUri { get; set; }

		/// <summary>
		/// URI of the detection's image file (.png) in blob storage.
		/// </summary>
		/// <example>https://storagesite.blob.core.windows.net/spectrogramspng/imagefilename.png</example>
		public string SpectrogramUri { get; set; }

		/// <summary>
		/// Location of the microphone that collected the detection.
		/// </summary>
		public Location Location { get; set; }

		/// <summary>
		/// Date and time of when the detection occurred.
		/// </summary>
		/// <example>2020-09-30T11:03:56.057346Z</example>
		public DateTime Timestamp { get; set; }

		/// <summary>
		/// List of sections within the detection that might contain whale sounds.
		/// </summary>
		public List<Annotation> Annotations { get; set; } = new List<Annotation>();

		/// <summary>
		/// Flag indicating whether or not the dection has been reviewed by a human moderator.
		/// </summary>
		/// <example>true</example>
		public bool Reviewed { get; set; }

		/// <summary>
		/// Flag indicating whether the human moderator heard whale sounds in the detection.
		/// </summary>
		/// <example>yes</example>
		public string Found { get; set; }

		/// <summary>
		/// Any text comments entered by the human moderator during review.
		/// </summary>
		/// <example>Clear whale sounds detected.</example> 
		public string Comments { get; set; }

		/// <summary>
		/// Calculated average confidence that the detection contains a whale sound.
		/// </summary>
		/// <example>84.39</example>
		public decimal Confidence { get; set; }

		/// <summary>
		/// Identity of the human moderator (User Principal Name for AzureAD) performing the review.
		/// </summary>
		/// <example>user@gmail.com</example>
		public string Moderator { get; set; }

		/// <summary>
		/// Date and time of when the detection was reviewed by the human moderator.
		/// </summary>
		/// <example>2020-09-30T11:03:56Z</example>
		public DateTime Moderated { get; set; }

		/// <summary>
		/// Any text comments entered by the human moderator during review (separated by semi-colon).
		/// </summary>
		/// <example>S7;S10</example>
		public string Tags { get; set; }

		/// <summary>
		/// Split tags into a list.
		/// </summary>
		/// <param name="tags">Tags string to split</param>
		/// <returns>List of tags</returns>
		public static List<string> GetTagList(string tags)
		{
			if (string.IsNullOrWhiteSpace(tags))
				return new List<string>();

			string[] delimiters = new string[] { ";", "," };
			var rawTags = tags.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
			var tagList = new List<string>(rawTags.Length);
			foreach (var rawTag in rawTags)
			{
				var trimmed = rawTag.Trim();
				if (trimmed.Length > 0)
					tagList.Add(trimmed);
			}
			return tagList;
		}

		/// <summary>
		/// Get the leaf tags from the given tags string.
		/// A leaf tag is a tag that does not have any child tags in the input list.
		/// </summary>
		/// <param name="tags"></param>
		/// <returns></returns>
		public static List<string> GetLeafTags(string tags)
		{
			List<string> tagList = GetTagList(tags);
			List<string> leafTags = new List<string>();
			foreach (var tag in tagList)
			{
				bool isLeaf = true;
				foreach (var pair in TagHierarchy)
				{
					if (pair.Value == tag && tagList.Contains(pair.Key))
					{
						isLeaf = false;
						break;
					}
				}
				if (isLeaf)
				{
					leafTags.Add(tag);
				}
			}
			return leafTags;
		}

		/// <summary>
		/// Tags in a list (parsed from the Tags string).
		/// </summary>
		public List<string> TagList => GetTagList(Tags);

		/// <summary>
		/// Hierarchy of tags, where the key is the child tag and the value is the parent tag.
		/// A null value indicates a top-level tag. Within tags at the same level, more likely
		/// entries should typically appear before less likely entries.
		/// </summary>
		public static readonly Dictionary<string, string> TagHierarchy = new Dictionary<string, string>()
		{
			{ "whale", null },
			{ "orca", "whale" },
			{ "srkw", "orca" },
			{ "J pod", "srkw" },
			{ "K pod", "srkw" },
			{ "L pod", "srkw" },
			{ "transient", "orca" },
			{ "humpback", "whale" },
			{ "vessel", null },
			{ "train", "vessel" },
			{ "bird", null },
			{ "pigu", "bird" },
			{ "kier", "bird" },
			{ "human", null },
			{ "jingle", null },
			{ "water", null },
			{ "hum", null },
		};

		/// <summary>
		/// List of suggested tags for the detection based on the machine prediction, the tag hierarchy,
		/// and the most recently moderated detection. Tags that are already in the TagList are not
		/// included in the suggestions.
		/// </summary>
		public List<string> SuggestedTagList
		{
			get
			{
				List<string> suggestions = new List<string>();

				// For each tag in the tags list, add any child tags not already in the tags list.
				var tagList = TagList;
				foreach (var tag in tagList)
				{
					foreach (var pair in TagHierarchy)
					{
						if (pair.Value == tag && !tagList.Contains(pair.Key))
						{
							suggestions.Add(pair.Key);
						}
					}
				}

				// Add any top-level tags not already in the tags list.
				foreach (var pair in TagHierarchy)
				{
					if (pair.Value == null && !tagList.Contains(pair.Key))
					{
						suggestions.Add(pair.Key);
					}
				}

				return suggestions;
			}
		}

		/// <summary>
		/// Machine-generated label for the detection based on the global prediction model.
		/// </summary>
		public string GlobalPredictionLabel { get; set; }

		/// <summary>
		/// AI Model that reported this detection.
		/// </summary>
		public string AIModel => string.IsNullOrEmpty(GlobalPredictionLabel) ? "OrcaHello" : "PODS-AI";
	}
}
