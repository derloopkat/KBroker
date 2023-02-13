using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;

namespace KBroker
{
    public class Configuration
    {
        public const string OrdersFileName = "operation.json";
        public static int Timeout;
        public static float IntervalSeconds;
        public static string Pair;
        public static Operation Operation;

        public static Operation LoadOrders()
        {
            try
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile(OrdersFileName, optional: false, reloadOnChange: true);
                var root = builder.Build();
                var simulationSection = root.GetSection("simulation");
                var operationSection = root.GetSection("operation");
                var pair = operationSection.GetSection("pair");
                var operationType = operationSection.GetSection("type").Value;
                var startPrice = operationSection.GetSection("startPrice");
                var useMarketPrice = operationSection.GetSection("useMarketPrice")?.Value ?? "false";
                var cancelOrders = operationSection.GetSection("cancelOrders");
                var version = root.GetSection("version");

                if (!pair.Exists())
                {
                    throw new Exception("Details: pair is missing. Please specify pair e.g. \"BTCUSD\".");
                }

                Timeout = int.Parse(root.GetSection("timeout").Value);
                IntervalSeconds = float.Parse(root.GetSection("interval").Value);
                Pair = pair.Value.ToUpper();

                if (operationType == "OneCancelsTheOther")
                {
                    var operation = new OneCancelsTheOther();
                    SetupStopLossOrder(operation, operationSection.GetSection("stoploss"));
                    SetupTakeProfitOrder(operation, operationSection.GetSection("takeprofit"));
                    Operation = operation;
                }
                else if (operationType == "TrailingStopLoss")
                {
                    var operation = new TrailingStopLoss();
                    SetupStopLossOrder(operation, operationSection.GetSection("stoploss"));
                    Operation = operation;
                }

                if (startPrice.Exists())
                {
                    Operation.StartPrice = decimal.Parse(startPrice.Value);
                }

                if (version.Exists())
                {
                    Operation.Version = float.Parse(root.GetSection("version").Value);
                }

                if (cancelOrders.Exists())
                {
                    Operation.CancelOrders = cancelOrders.GetChildren().Select(o => o.Value).ToArray();
                }

                if (simulationSection.Exists())
                {
                    var currentPrice = simulationSection.GetSection("currentPrice");
                    var priceTrend = simulationSection.GetSection("priceTrend");
                    Operation.Simulation = new Operation.SimulationConfiguration
                    {
                        CurrentPrice = decimal.Parse(currentPrice.Value),
                        PriceTrend = (SimulatedPriceTrend)Enum.Parse(typeof(SimulatedPriceTrend), priceTrend.Value)
                    };
                }

                Operation.UseMarketPrice = bool.Parse(useMarketPrice);
            }
            catch (Exception ex)
            {
                throw new ConfigurationException(ex.Message);
            }
            return Operation;
        }

        public static void LoadSecretKeys()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("keys.json", optional: false, reloadOnChange: true);
            var root = builder.Build();
            KrakenApi.ApiPrivateKey = root.GetSection("apiPrivateKey").Value;
            KrakenApi.ApiPublicKey = root.GetSection("apiPublicKey").Value;
        }

        private static void SetupTakeProfitOrder(Operation operation, IConfiguration section)
        {
            var id = section.GetSection("id");
            var volume = section.GetSection("volume");
            var greedy = section.GetSection("greedy")?.Value ?? "false";
            var plainGreed = section.GetSection("plainGreed")?.Value ?? "false";

            if (id.Exists())
            {
                operation.TakeProfit.Id = id.Value.Trim();
            }
            else
            {
                operation.TakeProfit.OrderType = OrderType.Market;
                operation.TakeProfit.SideType = OrderSide.Sell;
                operation.TakeProfit.Price = decimal.Parse(section.GetSection("price").Value);
                operation.TakeProfit.BeGreedy = bool.Parse(greedy);
                operation.TakeProfit.PlainGreed = bool.Parse(plainGreed);
                operation.TakeProfit.Pair = Configuration.Pair;

                if (volume.Exists())
                {
                    operation.TakeProfit.Volume = decimal.Parse(section.GetSection("volume").Value);
                }
                else if (operation.StopLoss.Volume.HasValue)
                {
                    operation.TakeProfit.Volume = operation.StopLoss.Volume;
                }
                else
                {
                    throw new Exception("Details: takeprofit volume is mandatory unless it's specified for stoploss, in which case the same is assumed.");
                }
            }
        }

        private static void SetupStopLossOrder(Operation operation, IConfigurationSection section)
        {
            var order = operation.StopLoss;
            var id = section.GetSection("id");
            var edit = section.GetSection("edit");
            var triggerBy = section.GetSection("triggerBy");
            var price = section.GetSection("price");
            var trailingLevels = section.GetSection("trailing");
            var volume = section.GetSection("volume");
            order.Pair = Pair;

            if (id.Exists())
            {
                order.Id = id.Value.Trim();
                order.IsPlaced = true;
            }
            else
            {
                if(!volume.Exists() || !price.Exists())
                {
                    throw new Exception("Details: price and volume are mandatory for creating a new order.");
                }

                order.OrderType = OrderType.StopLoss;
                order.SideType = OrderSide.Sell;
                order.Price = decimal.Parse(price.Value);
                order.Volume = decimal.Parse(volume.Value);
            }

            if (edit.Exists())
            {
                order.TriggerPrice = decimal.Parse(edit.GetSection("triggerPrice").Value);
                order.NewStoplossPrice = decimal.Parse(edit.GetSection("newPrice").Value);
            }

            if (triggerBy.Exists())
            {
                order.TriggerBy = triggerBy.Value == "index" ? OrderTriggerBy.Index : OrderTriggerBy.Last;
            }

            if (trailingLevels.Exists())
            {
                order.TrailingLevels = trailingLevels.GetChildren().Select(t => decimal.Parse(t.Value)).ToList();
            }
        }
    }
}
