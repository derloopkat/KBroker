using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;

namespace KBroker
{
    public class Configuration
    {
        public const float LatestConfigurationVersionSupported = 1.3f;
        public const string OrdersFileName = "operation.json";
        public const float MinimumIntervalForNonSimulatedEnvironment = 8f;
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
                var intervalSection = root.GetSection("interval");
                var rootStartPrice = root.GetSection("startPrice");
                var pair = operationSection.GetSection("pair");
                var operationType = operationSection.GetSection("type").Value;
                var startPrice = operationSection.GetSection("startPrice");
                var startVolume = operationSection.GetSection("startVolume");
                var useMarketPrice = operationSection.GetSection("useMarketPrice")?.Value ?? "false";
                var cancelOrders = operationSection.GetSection("cancelOrders");
                var version = root.GetSection("version");
                var isSimulation = simulationSection.Exists();

                if (!pair.Exists()) throw new Exception("Details: pair is missing. Please specify pair e.g. \"BTCUSD\".");
                if (rootStartPrice.Exists()) throw new Exception("Start price was declared at the wrong level.");

                Timeout = int.Parse(root.GetSection("timeout").Value);
                Pair = pair.Value.ToUpper();

                if (intervalSection.Exists())
                {
                    var interval = float.Parse(root.GetSection("interval").Value);
                    IntervalSeconds = isSimulation ? interval : Math.Max(interval, MinimumIntervalForNonSimulatedEnvironment);
                }
                else
                    IntervalSeconds = isSimulation ? 1 : MinimumIntervalForNonSimulatedEnvironment;

                if (operationType == "OneCancelsTheOther")
                {
                    var operation = new OneCancelsTheOther();
                    SetupStopLossOrder(operation, operationSection.GetSection("stoploss"));
                    SetupTakeProfitOrder(operation, operationSection.GetSection("takeprofit"));
                    Operation = operation;
                    if(operation.StopLoss.Price.HasValue && operation.TakeProfit.Price <= operation.StopLoss.Price + (operation.StopLoss.Price * 0.006m))
                    {
                        throw new Exception("Details: make sure takeprofit and stoploss prices are correct and the values are not too close.");
                    }
                }
                else if (operationType == "TrailingStopLoss")
                {
                    var operation = new TrailingStopLoss();
                    SetupStopLossOrder(operation, operationSection.GetSection("stoploss"));
                    Operation = operation;
                }

                Operation.StartPrice ??= startPrice.Exists() ? decimal.Parse(startPrice.Value) : null;
                Operation.StartVolume ??= startVolume.Exists() ? decimal.Parse(startVolume.Value) : null;
                Operation.Version ??= version.Exists() ? float.Parse(version.Value) : null;

                if(Operation.Version < LatestConfigurationVersionSupported)
                {
                    throw new Exception($"Your {OrdersFileName} file is no longer supported by this application. The latest supported version is {LatestConfigurationVersionSupported}.");
                }

                if(Operation.StartVolume < Operation.StopLoss.Volume || Operation.StartVolume < Operation.TakeProfit?.Volume)
                {
                    throw new Exception($"You can't sell more crypto than the amount available in your balance. Please check start volume, takeprofit and stoploss orders.");
                }

                if (cancelOrders.Exists()) Operation.CancelOrders = cancelOrders.GetChildren().Select(o => o.Value).ToArray();

                if (simulationSection.Exists())
                {
                    var milestones = simulationSection
                        .GetSection("milestones")
                        .GetChildren()
                        .Select(t => decimal.Parse(t.Value))
                        .ToList();
                    if (milestones.Count < 2)
                    {
                        throw new Exception("Simulation milestones must exist and contain at least two prices.");
                    }

                    Operation.Simulation = new Operation.SimulationConfiguration
                    {
                        Milestones = milestones,
                        Trend = milestones[0] < milestones[1] ? SimulatedPriceTrend.Ascending
                            : SimulatedPriceTrend.Descending
                    };
                    Simulator.CurrentPrice = milestones.First();
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
            var order = operation.TakeProfit;
            var volume = section.GetSection("volume");
            var price = section.GetSection("price");
            var greedy = section.GetSection("greedy")?.Value ?? "false";
            var plainGreed = section.GetSection("plainGreed")?.Value ?? "false";
            var trigger = section.GetSection("trigger");

            if (!price.Exists())
            {
                throw new Exception("Details: price is mandatory for your takeprofit order.");
            }

            order.OrderType = OrderType.Market;
            order.SideType = OrderSide.Sell;
            order.Price = decimal.Parse(price.Value);
            order.BeGreedy = bool.Parse(greedy);
            order.PlainGreed = bool.Parse(plainGreed);
            order.Pair = Pair;

            if (volume.Exists())
            {
                order.Volume = decimal.Parse(section.GetSection("volume").Value);
            }
            else if (operation.StopLoss.Volume.HasValue)
            {
                order.Volume = operation.StopLoss.Volume;
            }
            else
            {
                throw new Exception("Details: takeprofit volume is mandatory unless it's the same specified by stoploss in your operation file.");
            }

            if (trigger.Exists())
            {
                order.Trigger.Price = decimal.Parse(trigger.GetSection("price").Value);
                order.Trigger.NewPrice = decimal.Parse(trigger.GetSection("newPrice").Value);
            }
        }

        private static void SetupStopLossOrder(Operation operation, IConfigurationSection section)
        {
            var order = operation.StopLoss;
            var id = section.GetSection("id");
            var trigger = section.GetSection("trigger");
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
            else if (!volume.Exists() || !price.Exists())
            {
                throw new Exception("Details: price and volume are mandatory for creating a new order.");
            }
            else
            {
                order.OrderType = OrderType.StopLoss;
                order.SideType = OrderSide.Sell;
                order.Price = decimal.Parse(price.Value);
                order.Volume = decimal.Parse(volume.Value);
            }

            if (trigger.Exists())
            {
                order.Trigger.Price = decimal.Parse(trigger.GetSection("price").Value);
                order.Trigger.NewPrice = decimal.Parse(trigger.GetSection("newPrice").Value);
            }

            if (triggerBy.Exists())
            {
                order.TriggerBy = triggerBy.Value == "index" ? OrderTriggerBy.Index : OrderTriggerBy.Last;
            }

            if (trailingLevels.Exists())
            {
                order.TrailingLevels = trailingLevels.GetChildren().Select(t => decimal.Parse(t.Value)).ToList();
                if (!((TrailingStopLoss)operation).LevelsAreConsistent())
                {
                    throw new Exception("Details: trailing levels must include two or more prices, sorted ascending.");
                }
            }
            else if(operation is TrailingStopLoss)
            {
                throw new Exception("Details: trailing levels are mandatory for trailing stoploss operations.");
            }
        }
    }
}
