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
		/// Machine-generated label for the detection based on the global prediction model.
		/// </summary>
		public string GlobalPredictionLabel { get; set; }
	}
}
