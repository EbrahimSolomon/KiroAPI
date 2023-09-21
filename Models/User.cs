using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KironAPI.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string Salt {get; set; }
    }
}