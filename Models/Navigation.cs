using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KironAPI.Models
{
    public class Navigation
    {
           public int ID { get; set; }
    public string Text { get; set; }
    public int ParentID { get; set; }
    public List<Navigation> Children { get; set; } = new List<Navigation>();
    }
}