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

        public string Pair
        {
            get
            {
                return Configuration.Pair;
            }
            set
            {
                Configuration.Pair = value;
            }
        }

        public int IntervalMiliseconds
        {
            get
            {
                return (int)(Configuration.IntervalSeconds * 1000);
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
                ulong currentPriceId = LastPriceId;
                string jsonResponse = KrakenApi.QueryPublicEndpoint("OHLC", $"pair={Pair}&since={LastPriceId}").Result;
                var body = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                errors = body.error.ToObject<string[]>();

                if (errors.Length == 0)
                {
                    var alterPair = "X" + Pair.Substring(0, Pair.Length - 3) + "Z" + Pair.Substring(Pair.Length - 3);
                    var pairs = body.result[Pair] ?? body.result[alterPair];
                    foreach (var pair in pairs)
                    {
                        var price = new Price(pair);
                        currentPriceId = price.Id;
                        Prices[currentPriceId] = price;
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

        public void WaitForStartPrice(string pair, decimal startPrice)
        {
            decimal price = 0;
            var symbol = Display.GetCurrencySymbol(pair);
            while (startPrice > price)
            {
                try
                {
                    price = GetCurrentPrice().Close;
                    Display.PrintWaitingForStartPrice(Pair, price, InitialPrice.Value, startPrice);
                    Console.Title = $"Awaiting {Pair}...";
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
            Logger.AddEntry($"AddOrder: {order.QueryString} \r\n{Convert.ToString(response)}");
            return response;
        }

        public virtual dynamic EditOrder(Order order)
        {
            order.IsEdit = true;
            var json = KrakenApi.QueryPrivateEndpoint("EditOrder", order.QueryString).Result;
            var response = JsonConvert.DeserializeObject<dynamic>(json);
            order.Error = response["error"].Count > 0 || response["result"]["status"].Value != "ok";
            order.IsUnknown = response["error"].ToString().Contains("EOrder:Unknown order");
            if (!order.IsUnknown)
            {
                order.Id = response["result"]?["txid"]?.Value ?? order.Id;
            }
            Logger.AddEntry($"AddOrder: {order.QueryString} {Environment.NewLine}{Convert.ToString(response)}");
            return response;
        }

        public virtual dynamic QueryOrder(Order order)
        {
            var json = KrakenApi.QueryPrivateEndpoint("QueryOrders", order.QueryString).Result;
            var response = JsonConvert.DeserializeObject<dynamic>(json);
            order.IsClosed = response["result"]?[order.Id]?["status"] == "closed";
            order.IsCanceled = response["result"]?[order.Id]?["status"] == "canceled";
            order.IsOkay = response["result"]?[order.Id]?["status"] == "ok";
            order.IsOpen = response["result"]?[order.Id]?["status"] == "open";
            order.IsUnknown = response["error"].ToString().Contains("EOrder:Unknown order");
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

        public bool PriceLostTooMuchGains(decimal currentPrice, decimal takeProfitPrice)
        {
            TimeSpan timeSinceMaxWasSet = MaxPrice.Created.HasValue ? MaxPrice.Created.Value - DateTime.Now : TimeSpan.MaxValue;
            decimal seekToKeepAtLeastThisPercentageFromGains = timeSinceMaxWasSet.TotalMinutes < 3 ? 30
                : timeSinceMaxWasSet.TotalMinutes < 5 ? 40
                : timeSinceMaxWasSet.TotalMinutes < 8 ? 60
                : timeSinceMaxWasSet.TotalMinutes < 10 ? 75
                : 80;
            var minimumAcceptableGains = (MaxPrice.Close - takeProfitPrice) * seekToKeepAtLeastThisPercentageFromGains / 100;
            var minimumAcceptablePrice = takeProfitPrice + minimumAcceptableGains;
            return currentPrice < minimumAcceptablePrice;
        }
    }
}
