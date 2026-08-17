using System;
using System.Collections.Generic;

namespace AIForOrcas.Server.BL.Models.CosmosDB
{
    public class Metadata
    {
        public string id { get; set; }
        public string source_guid { get; set; }
        public string audioUri { get; set; }
        public string imageUri { get; set; }
        public bool reviewed { get; set; }
        public DateTime timestamp { get; set; }
        public decimal whaleFoundConfidence { get; set; }
        public Location location { get; set; }
        public List<Prediction> predictions { get; set; } = new List<Prediction>();
        public string SRKWFound { get; set; }
        public string comments { get; set; }
        public string dateModerated { get; set; }
        public string moderator { get; set; }
        public string tags { get; set; }
        public string globalPredictionLabel { get; set; }
    }
}
