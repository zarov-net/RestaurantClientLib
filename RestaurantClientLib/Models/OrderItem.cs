using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestaurantClientLib.Models
{
    public class OrderItem
    {
        public string DishCode { get; set; }
        public int Quantity { get; set; }
    }
}