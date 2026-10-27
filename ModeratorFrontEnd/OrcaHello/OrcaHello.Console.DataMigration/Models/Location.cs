namespace OrcaHello.Console.DataMigration.Models
{
    public class Location
    {
        public string id { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public double longitude { get; set; }
        public double latitude { get; set; }
    }
}
