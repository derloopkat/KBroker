using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace KBroker
{
    public class Price
    {
        public ulong Id { get; set; }
        public decimal Open { get; set; }
        public decimal Close { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Average { get; set; }
        public decimal Volume { get; set; }
        public decimal Count { get; set; }
        public DateTime? Created { get; set; }

        public Price(JArray pair)
        {
            Id = Convert.ToUInt64(pair[0]);
            Open = Convert.ToDecimal(pair[1]);
            High = Convert.ToDecimal(pair[2]);
            Close = Convert.ToDecimal(pair[3]);
            Average = Convert.ToDecimal(pair[4]);
            Volume = Convert.ToDecimal(pair[5]);
            Count = Convert.ToDecimal(pair[6]); ;
        }
        public Price(decimal price, DateTime? created = null)
        {
            Close = price;
            Created = created;
        }
    }
}
