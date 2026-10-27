namespace OrcaHello.Console.DataMigration.Models
{
    public class Metadata
    {
        public string id { get; set; } = string.Empty;
        public string source_guid { get; set; } = string.Empty;
        public string audioUri { get; set; } = string.Empty;
        public string imageUri { get; set; } = string.Empty;
        public bool reviewed { get; set; }
        public DateTime timestamp { get; set; }
        public decimal whaleFoundConfidence { get; set; }
        public Location? location { get; set; }
        public List<Prediction> predictions { get; set; } = new List<Prediction>();
        public string SRKWFound { get; set; } = string.Empty;
        public string comments { get; set; } = string.Empty;
        public string dateModerated { get; set; } = string.Empty;
        public string moderator { get; set; } = string.Empty;
        public string tags { get; set; } = string.Empty;
    }
}
