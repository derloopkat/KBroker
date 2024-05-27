using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KBroker
{
    public class Simulator : Broker
    {
        public static Dictionary<string,Order> Orders = new Dictionary<string, Order>();
        private static Random RandomValue = new Random();
        public static decimal CurrentPrice { get; set; }

        public override int PercentageDone { get; set; }

        public Simulator(decimal? stopLossPrice = null)
        {
            var ids = new[] { "XXXXXX-XXXXX-123456", "XXXXXX-XXXXX-000001", "XXXXXX-XXXXX-000002", "XXXXXX-PRICE-00014" };
            if (stopLossPrice.HasValue)
            {
                Orders.Add(ids.First(), new Order(ids.First())
                {
                    OrderType = OrderType.StopLoss,
                    SideType = OrderSide.Sell,
                    Volume = 1,
                    IsOpen = true,
                    Price = stopLossPrice
                });
            }
            Orders.Add(ids[1], new Order(ids[1])
            {
                OrderType = OrderType.Limit,
                SideType = OrderSide.Sell,
                Price = stopLossPrice * 1.1m,
                Volume = 0.5m,
                IsOpen = true
            });
            Orders.Add(ids[2], new Order(ids[2])
            {
                OrderType = OrderType.Limit,
                SideType = OrderSide.Sell,
                Volume = 0.5m,
                IsOpen = true,
                Price = stopLossPrice * 1.2m,
            });
            Orders.Add(ids[3], new Order(ids[3])
            {
                OrderType = OrderType.StopLoss,
                SideType = OrderSide.Sell,
                IsOpen = true,
                Price = 14,
                Volume = 1m
            });

            var buyOrder = new Order("XXXXXX-XXBUY-000013")
            {
                OrderType = OrderType.Limit,
                SideType = OrderSide.Buy,
                IsOpen = true,
                Price = 13,
                Volume = 5m
            };
            Orders.Add(buyOrder.Id, buyOrder);
            MaxPrice = new Price(0);
        }

        public override dynamic GetAccountBalances()
        {
            const string buyOrderId = "XXXXXX-XXBUY-000013";
            var buyOrder = Orders.ContainsKey(buyOrderId) ? Orders[buyOrderId] : null;
            var marketPrice = GetCurrentPrice();
            var balance = buyOrder?.Price >= marketPrice.Close ? buyOrder?.Volume : 0;
            var json = $"{{\"error\":[],\"result\":{{\"FOO\":\"{balance}\"}}}}";
            var response = JsonConvert.DeserializeObject<dynamic>(json);

            if (balance > 0)
            {
                buyOrder.IsCompleted = true;
                Orders.Remove(buyOrderId);
            }
            return response;
        }

        public override Price GetCurrentPrice()
        {
            if (Prices != null)
            {
                var simulation = (Operation.SimulationConfiguration)Configuration.Operation.Simulation;
                var milestone = simulation.Milestones.First();
                var step = Math.Round(CurrentPrice * 0.005M, 4, MidpointRounding.AwayFromZero);

                if (simulation.Trend == SimulatedPriceTrend.Ascending)
                {
                    CurrentPrice += step;
                }

                else if (simulation.Trend == SimulatedPriceTrend.Descending)
                {
                    CurrentPrice -= step;

                }

                var priceReachedMilestone = (simulation.Trend == SimulatedPriceTrend.Ascending && CurrentPrice >= milestone)
                    || (simulation.Trend == SimulatedPriceTrend.Descending && CurrentPrice <= milestone);
                if (priceReachedMilestone && simulation.Milestones.Count > 1)
                {
                    simulation.Milestones.Remove(milestone);

                    var nextMilestone = simulation.Milestones.First();
                    simulation.Trend = milestone < nextMilestone ? SimulatedPriceTrend.Ascending
                        : milestone > nextMilestone ? SimulatedPriceTrend.Descending
                        : (SimulatedPriceTrend)((int)simulation.Trend * -1);
                }

                LastPriceId = (ulong)Prices.Values.Count;
                Prices[LastPriceId] = new Price(CurrentPrice);
            }

            MaxPrice = CurrentPrice > MaxPrice.Close ? new Price(CurrentPrice) : MaxPrice;
            InitialPrice ??= CurrentPrice;

            return new Price(CurrentPrice);
        }

        public override dynamic AddOrder(Order order)
        {
            dynamic response = null;

            if (ConfirmOrder(order))
            {
                order.Id = $"XXXXXX-XXXXX-{Orders.Values.Count.ToString("######")}";
                string json = $"{{\"error\": [ ],\"result\": {{\"descr\": {{\"order\": \"{order.SideType.GetDescription()} {order.Volume} {Configuration.Pair} @ {order.OrderType.GetDescription()} {order.Price}\"}},\"txid\": [\"{order.Id}\"]}}}}";
                Orders[order.Id] = order;
                response = JsonConvert.DeserializeObject<dynamic>(json);
                order.Error = response["error"].Count > 0;
                order.IsPlaced = !order.Error;
                return response;
            }
            else
                Environment.Exit(0);
            return response;
        }

        public override dynamic CancelOrder(Order order)
        {
            string json;
            if (Orders.ContainsKey(order.Id) && !Orders[order.Id].IsClosed)
            {
                Orders.Remove(order.Id);
                json = "{\"error\": [ ], \"result\": { \"count\": 1 }}";
            }
            else
            {
                json = "{\"error\": [ \"EOrder:Unknown order\" ], \"result\": { \"count\": 0 }}";
            }
            var response = JsonConvert.DeserializeObject<dynamic>(json);
            order.Error = response["error"].Count > 0;
            order.IsUnknown = response["error"].ToString().Contains("EOrder:Unknown order");
            order.IsClosed = response["result"]?["count"]?.Value == 1 || order.IsUnknown;
            return response;
        }

        public override dynamic QueryOrder(Order order)
        {
            string json;
            var currentPrice = GetCurrentPrice();
            if (Orders.ContainsKey(order.Id))
            {
                var remote = Orders[order.Id];
                order.OrderType = remote.OrderType;
                order.SideType = remote.SideType;
                order.Volume = remote.Volume;
                order.Price = remote.Price;
                order.IsOkay = true;
                order.IsOpen = true;
            }
            if(order.OrderType == OrderType.Market && order.IsPlaced)
            {
                order.IsClosed = true;
                order.IsCompleted = true;
            }
            else if(order.SideType == OrderSide.Sell && order.IsPlaced && currentPrice.Close <= order.Price) 
            {
                /* TODO: to make it more realistic check datetime and history of prices before marking as sold. It could be that market price reached limit and went up. */
                order.IsClosed = true;
                order.IsCompleted = true;
            }

            if (Orders.ContainsKey(order.Id))
            {
                var orderPrice = order.OrderType == OrderType.Market ? currentPrice.Close : order.Price.Value;
                json =  $"{{\"error\": [ ],\"result\": {{\"{order.Id}\": {{\"status\": \"{(order.IsClosed ? "closed" : "open")}\"," +
                        $"\"opentm\": {DateTimeOffset.UtcNow.ToUnixTimeSeconds()},\"descr\": {{\"pair\": \"{Configuration.Pair}\",\"type\": \"{order.SideType.GetDescription()}\"," +
                        $"\"ordertype\": \"{order.OrderType.GetDescription()}\",\"price\": \"{orderPrice}\",\"order\": \"{order.SideType.GetDescription()} " +
                        $"{order.Volume} {Configuration.Pair} @ {order.OrderType.GetDescription()} {orderPrice}\",\"close\": \"\"}},\"vol\": \"{order.Volume}\", \"vol_exec\": " +
                        $"\"{order.Volume}\",\"cost\": \"{orderPrice * order.Volume}\",\"fee\": \"XX.X\",\"price\": \"{orderPrice}\",\"stopprice\": \"0.00000\"," +
                        $"\"limitprice\": \"{orderPrice}\",\"trigger\": \"index\"}}}}}}";
            }
            else
            {
                json = $"{{\"error\": [ \"EOrder:Unknown order\" ]}}";
            }
            var response = JsonConvert.DeserializeObject<dynamic>(json);
            var cost = response["result"]?[order.Id]?["cost"];
            if (!String.IsNullOrEmpty(cost?.Value))
            {
                order.Cost = (decimal)cost;
            }
            return response;
        }

        public override dynamic EditOrder(Order order)
        {
            string json;
            order.IsEdit = true;
            if (order.HasId && Orders.ContainsKey(order.Id))
            {
                var status = "ok";
                var newOrderId = $"XXXXXX-XXXXX-{DateTime.Now.Ticks}";
                json = $"{{ \"error\": [], \"result\": {{ \"status\": \"{status}\",  \"txid\": \"{newOrderId}\",  \"originaltxid\": \"{order.Id}\",  \"price\": \"{order.Price}\",  \"orders_cancelled\": 1,  \"descr\": {{   \"order\": \"{order.SideType.GetDescription()} {order.Volume} {Configuration.Pair} @ limit {order.Price}\"  }} }}}} ";

            }
            else
            {
                json = $"{{\"error\": [ \"EOrder:Unknown order\" ]}}";
            }

            var response = JsonConvert.DeserializeObject<dynamic>(json);
            order.Error = response["error"].Count > 0 || response["result"]["status"].Value != "ok";
            order.IsUnknown = response["error"].ToString().Contains("EOrder:Unknown order");
            if (!order.IsUnknown)
            {
                order.Id = response["result"]?["txid"]?.Value ?? order.Id;
            }
            if (!order.Error)
            {
                Orders[order.Id] = order;
                // TODO: mark the previous order as cancelled and closed in Orders dictionary
            }

            return response;
        }
    }
}
