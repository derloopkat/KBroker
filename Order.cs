using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KBroker
{
    public class Order
    {
        public string Id { get; set; }
        public bool IsClosed { get; set; }
        public bool IsCanceled { get; set; }
        public bool IsPlaced { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsUnknown { get; set; }
        public bool IsEdit { get; set; }
        public bool IsOkay { get; set; }
        public bool IsOpen { get; set; }
        public bool IsShadow { get; set; }
        public bool BeGreedy { get; set; }
        public bool PlainGreed { get; set; }
        public bool Error { get; set; }
        public bool Validate { get; set; }
        public decimal? Price { get; set; }
        public string Pair { get; set; }
        public decimal? Volume { get; set; }
        public decimal? Cost { get; set; }
        public OrderType? OrderType { get; set; }
        public OrderSide? SideType { get;set; }
        public decimal? TriggerPrice { get; set; }
        public decimal? NewStoplossPrice { get; set; }
        public OrderTriggerBy? TriggerBy { get; set; }
        public List<decimal> TrailingLevels { get; set; }

        public decimal? NextTrailingPrice 
        {
            get
            {
                return TrailingLevels.Count > 0 ? TrailingLevels[0] : null;
            }
        }

        public decimal? TriggerTrailingPrice
        {
            get
            {
                return TrailingLevels.Count > 1 ? TrailingLevels[1] : null;
            }
        }

        public Order(string id)
        {
            Id = id;
        }

        public Order()
        {
            TrailingLevels = new List<decimal>();
        }

        public Dictionary<string,string> PostData
        {
            get
            {
                if (!string.IsNullOrEmpty(Id))
                {
                    if (IsEdit)
                    {
                        var post = new Dictionary<string, string>()
                        {
                            { "txid", Id },
                            { "pair", Configuration.Pair },
                            { "price", Price.ToString() }
                        };
                        if (Volume.HasValue)
                        {
                            post["volume"] = Volume.ToString();
                        }
                        return post;
                    }
                    else
                    {
                        return new Dictionary<string, string>()
                        {
                            { "txid", Id }
                        };
                    }
                }
                else
                {
                    var post = new Dictionary<string, string>()
                    {
                        { "ordertype", OrderType.GetDescription() },
                        { "pair", Configuration.Pair },
                        { "type", SideType.GetDescription() },
                        { "volume", Volume.ToString() }
                    };

                    if(OrderType != KBroker.OrderType.Market)
                    {
                        post.Add("price", Price.ToString());
                    }

                    if (TriggerBy.HasValue)
                    {
                        post.Add("trigger", TriggerBy.GetDescription());
                    }

                    if (Validate)
                    {
                        post.Add("validate", true.ToString());
                    }
                    return post;
                }
            }
        }

        public string QueryString
        {
            get
            {
                return string.Join("&", PostData.Select(e => e.Key + "=" + e.Value).ToArray());
            }
        }
    }
}
