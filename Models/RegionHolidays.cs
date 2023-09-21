using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KironAPI.Models
{
    public class RegionHolidays
    {
            public string Division { get; set; }
        public List<HolidayEvent> Events { get; set; }
    }
}