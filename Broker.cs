using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace KBroker
{
    public class Broker
    {
        public ulong LastPriceId = 0;
        public DateTime? LastPriceUpdate = null;
        public Price MaxPrice { get; set; }
        public virtual int PercentageDone { get; set; }
        public Dictionary<ulong, Price> Prices { get; set; }
        public decimal? InitialPrice { get; set; }

        public enum SystemStatus
        {
            Online,
            Maintenance,
            CancelOnly,
            PostOnly,
            OtherErrors
        }

        public Broker()
        {
            Prices = new Dictionary<ulong, Price>();
            MaxPrice = new Price(0);
        }

        public int IntervalMiliseconds
        {
            get
            {
                return (int)(Configuration.IntervalSeconds * 1000);
            }
        }

        public void Trade(Operation operation)
        {
            int seconds(double value) => (int)(value * 1000);
            while (!operation.TasksCompleted)
            {
                var done = this.PercentageDone;
                var wait = done >= 93 && done <= 95 ? Math.Min(seconds(8), IntervalMiliseconds)
                        : done >= 96 && done <= 104 ? Math.Min(seconds(operation.TakeProfit.PlainGreed ? 6 : 3), IntervalMiliseconds)
                        : done >= 105 && done <= 110 ? Math.Min(seconds(8), IntervalMiliseconds)
                        : done >= 111 && done <= 120 ? Math.Min(seconds(16), IntervalMiliseconds)
                        : IntervalMiliseconds;
                //System.Diagnostics.Debug.Print(wait.ToString());
                operation.Execute(this);
                Thread.Sleep(wait);
            }
        }

        public SystemStatus GetSystemStatus()
        {
            string json = KrakenApi.QueryPublicEndpoint("SystemStatus").Result;
            var response = JsonConvert.DeserializeObject<dynamic>(json);
            //string[] errors = response.error.ToObject<string[]>();
            //if (errors.Length > 0)
            //{
            //    return SystemStatus.OtherErrors;
            //}
            //else
            //{
            return response.result?.status?.Value switch
            {
                "online" => SystemStatus.Online,
                "cancel_only" => SystemStatus.CancelOnly,
                "post_only" => SystemStatus.PostOnly,
                "maintenance" => SystemStatus.Maintenance,
                _ => SystemStatus.OtherErrors,
            };
            //}
        }

        public void UpdatePrices()
        {
            string[] errors = null;
            try
            {
                var pair = Configuration.Pair;
                ulong currentPriceId = LastPriceId;
                string jsonResponse = Configuration.Operation.UseMarketPrice ?
                    KrakenApi.QueryPublicEndpoint("Ticker", $"pair={pair}").Result :
                    KrakenApi.QueryPublicEndpoint("OHLC", $"pair={pair}&since={LastPriceId}").Result;
                var body = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                errors = body.error.ToObject<string[]>();

                if (errors.Length == 0)
                {
                    var alterPair = "X" + pair.Substring(0, pair.Length - 3) + "Z" + pair.Substring(pair.Length - 3);
                    var pricePairs = body.result[pair] ?? body.result[alterPair];
                    if (Configuration.Operation.UseMarketPrice)
                    {
                        var price = new Price(decimal.Parse(pricePairs.b.First.Value));
                        currentPriceId = (ulong)DateTime.Now.Ticks;
                        Prices[currentPriceId] = price;
                    }
                    else
                    {
                        foreach (var pricePair in pricePairs)
                        {
                            var price = new Price(pricePair);
                            currentPriceId = price.Id;
                            Prices[currentPriceId] = price;
                        }
                    }
                    LastPriceId = currentPriceId;
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error retrieving price from server. {ex.Message} {ex.InnerException?.Message}";
                throw new Exception(errorMessage);
            }
            finally
            {
                if (errors?.Length > 0)
                {
                    Display.PrintErrors(errors);
                    if (errors.Contains("EGeneral:Too many requests"))
                    {
                        Thread.Sleep(10000);
                    }
                }
            }
        }

        public virtual Price GetCurrentPrice()
        {
            if (!LastPriceUpdate.HasValue || DateTime.Now.Subtract(LastPriceUpdate.Value).TotalMinutes >= Configuration.IntervalSeconds)
            {
                UpdatePrices();
            }

            Price currentPrice = Prices[LastPriceId];
            MaxPrice = currentPrice.Close > MaxPrice.Close ? currentPrice : MaxPrice;
            InitialPrice ??= currentPrice.Close;

            return currentPrice;
        }

        public Price GetLastPrice()
        {
            return LastPriceId == 0 ? null : Prices[LastPriceId];
        }

        public void WaitForStartPrice(decimal startPrice)
        {
            decimal price = 0;
            var symbol = Display.GetCurrencySymbol();
            while (startPrice > price)
            {
                try
                {
                    price = GetCurrentPrice().Close;
                    Display.PrintWaitingForStartPrice(price, InitialPrice.Value, startPrice);
                    Console.Title = $"Awaiting {Configuration.Pair}...";
                }
                catch (Exception ex)
                {
                    Display.PrintError(ex.Message);
                    Logger.AddEntry(ex.Message + $"\r\n{ex.StackTrace}");
                }
                finally
                {
                    Thread.Sleep(IntervalMiliseconds);
                }
            }
            Display.Print("");
        }

        public bool ConfirmOrder(Order order)
        {
            if (DateTime.Now.Subtract(Program.FirstRun) < TimeSpan.FromSeconds(5))
            {
                var currentPrice = GetCurrentPrice();
                var symbol = Display.GetCurrencySymbol(order.Pair);
                var details = $"{order.SideType} {order.OrderType.GetDescription()} Price: {symbol}{order.Price} Volume: {order.Volume}";
                if ((order.OrderType == OrderType.StopLoss && order.Price >= currentPrice.Close) || 
                    (order.OrderType == OrderType.Market && order.Price <= currentPrice.Close) ||
                    (order.OrderType != OrderType.StopLoss && order.OrderType != OrderType.Market ))
                {
                    Display.Print($"WARNING: the current MARKET price is {symbol}{currentPrice.Close}. " +
                                  $"Your {order.SideType.ToString().ToUpper()} order will be executed immediatelly. " +
                                  $@"Make sure the ""{order.OrderType.GetDescription()}"" order price {symbol}{order.Price} is correct.", ConsoleColor.Yellow);
                }
                Display.Print($"Confirm submitting order {details} (Y/N) ", ConsoleColor.Cyan, true);
                bool confirmed = Console.ReadKey().Key == ConsoleKey.Y;
                Display.Print(Environment.NewLine);
                return confirmed;
            }
            return true;
        }

        public virtual dynamic AddOrder(Order order)
        {
            dynamic response = null;
            if (ConfirmOrder(order))
            {
                var json = KrakenApi.QueryPrivateEndpoint("AddOrder", order.QueryString).Result;
                response = JsonConvert.DeserializeObject<dynamic>(json);
                order.Error = response["error"].Count > 0;
                order.IsPlaced = !order.Error;
                if (!order.Error && !order.Validate)
                {
                    order.Id = response["result"]["txid"][0].Value;
                }
                Logger.AddEntry($"AddOrder: {order.QueryString} \r\n{Convert.ToString(response)}");
            }
            else
                Environment.Exit(0);
            return response;
        }

        public virtual dynamic CancelOrder(Order order)
        {
            var json = KrakenApi.QueryPrivateEndpoint("CancelOrder", order.QueryString).Result;
            var response = JsonConvert.DeserializeObject<dynamic>(json);
            order.Error = response["error"].Count > 0;
            order.IsUnknown = response["error"].ToString().Contains("EOrder:Unknown order");
            order.IsClosed = response["result"]?["count"] == 1 || order.IsUnknown;
            Logger.AddEntry($"CancelOrder: {order.QueryString} \r\n{Convert.ToString(response)}");
            return response;
        }

        public virtual dynamic EditOrder(Order order)
        {
            order.IsEdit = true;
            var json = KrakenApi.QueryPrivateEndpoint("EditOrder", order.QueryString).Result;
            var response = JsonConvert.DeserializeObject<dynamic>(json);
            order.Error = response["error"].Count > 0 || response["result"]?["status"]?.Value != "ok";
            order.IsUnknown = response["error"].ToString().Contains("EOrder:Unknown order");
            if (!order.IsUnknown)
            {
                order.Id = response["result"]?["txid"]?.Value ?? order.Id;
            }
            Logger.AddEntry($"EditOrder: {order.QueryString} {Environment.NewLine}{Convert.ToString(response)}");
            return response;
        }

        public virtual dynamic QueryOrder(Order order)
        {
            var json = KrakenApi.QueryPrivateEndpoint("QueryOrders", order.QueryString).Result;
            var response = JsonConvert.DeserializeObject<dynamic>(json);
            var details = response["result"]?[order.Id];
            var status = details?["status"];
            order.IsClosed = status == "closed";
            order.IsCanceled = status == "canceled";
            order.IsOkay = status == "ok";
            order.IsOpen = status == "open";
            order.IsUnknown = response["error"].ToString().Contains("EOrder:Unknown order");
            //order.TriggerBy = details["trigger"] == "index" ? OrderTriggerBy.Index : OrderTriggerBy.Last;
            //order.Cost = response["result"]?[order.Id]?["cost"];
            if (order.IsClosed)
            {
                order.IsCompleted = response["result"][order.Id]["vol"] == response["result"][order.Id]["vol_exec"];
                if (order.IsCompleted)
                {
                    order.Cost = response["result"][order.Id]["cost"];
                }
            }
            Logger.AddEntry($"QueryOrders: {order.QueryString} {Environment.NewLine}{Convert.ToString(response)}");
            return response;
        }

        public bool PriceLostTooMuchGain(decimal currentPrice, decimal takeProfitPrice, bool useMarketPrice)
        {
            TimeSpan timeSinceMaxWasSet = MaxPrice.Created.HasValue ? MaxPrice.Created.Value - DateTime.Now : TimeSpan.MaxValue;
            decimal seekToKeepAtLeastThisPercentageFromGains = 
                  timeSinceMaxWasSet.TotalMinutes < 3 ? (useMarketPrice ? 7  : 20)
                : timeSinceMaxWasSet.TotalMinutes < 5 ? (useMarketPrice ? 15 : 40)
                : timeSinceMaxWasSet.TotalMinutes < 8 ? (useMarketPrice ? 50 : 60)
                : timeSinceMaxWasSet.TotalMinutes < 10 ? 70
                : 75;
            var minimumAcceptableGains = (MaxPrice.Close - takeProfitPrice) * seekToKeepAtLeastThisPercentageFromGains / 100;
            var minimumAcceptablePrice = takeProfitPrice + minimumAcceptableGains;
            return currentPrice < minimumAcceptablePrice;
        }
    }
}
