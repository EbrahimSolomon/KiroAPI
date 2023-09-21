using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KironAPI.Models
{
    public class CoinModel
    {
        [JsonPropertyName("england-and-wales")]
        public HolidayDivision EnglandAndWales { get; set; }

        [JsonPropertyName("scotland")]
        public HolidayDivision Scotland { get; set; }

        [JsonPropertyName("northern-ireland")]
        public HolidayDivision NorthernIreland { get; set; }

        public class HolidayDivision
        {
            [JsonPropertyName("division")]
            public string Division { get; set; }

            [JsonPropertyName("events")]
            public List<Event> Events { get; set; }
        }

        public class Event
        {
            [JsonPropertyName("title")]
            public string Title { get; set; }

            [JsonPropertyName("date")]
            public DateTime Date { get; set; }

            [JsonPropertyName("notes")]
            public string Notes { get; set; }

            [JsonPropertyName("bunting")]
            public bool Bunting { get; set; }
        }
    }
}
