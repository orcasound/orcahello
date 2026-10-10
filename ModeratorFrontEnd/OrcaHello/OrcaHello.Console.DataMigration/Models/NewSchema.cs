namespace OrcaHello.Console.DataMigration.Models
{
    public class Metadata2
    {
        public string id { get; set; } = string.Empty;
        public string state { get; set; } = string.Empty;
        public string locationName { get; set; } = string.Empty;
        public string audioUri { get; set; } = string.Empty;
        public string imageUri { get; set; } = string.Empty;
        public DateTime timestamp { get; set; }
        public decimal whaleFoundConfidence { get; set; }
        public Location? location { get; set; }
        public List<Prediction> predictions { get; set; } = new List<Prediction>();
        public string comments { get; set; } = string.Empty;
        public string dateModerated { get; set; } = string.Empty;
        public string moderator { get; set; } = string.Empty;
        public List<string> tags { get; set; } = new List<string>();
    }
}
