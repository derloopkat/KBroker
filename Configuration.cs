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
                var operationSection = root.GetSection("operation");
                var operationType = operationSection.GetSection("type").Value;
                var startPrice = operationSection.GetSection("startPrice");
                var version = root.GetSection("version");
                Timeout = int.Parse(root.GetSection("timeout").Value);
                IntervalSeconds = float.Parse(root.GetSection("interval").Value);
                Pair = operationSection.GetSection("pair").Value;
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
                LoadKeys();
            }
            catch (Exception ex)
            {
                throw new ConfigurationException(ex.Message);
            }
            return Operation;
        }

        private static void LoadKeys()
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
            if (id.Exists())
            {
                operation.TakeProfit.Id = id.Value.Trim();
            }
            else
            {
                var volume = section.GetSection("volume");
                var greedy = section.GetSection("greedy")?.Value ?? "false";
                var plainGreed = section.GetSection("plainGreed")?.Value ?? "false";
                operation.TakeProfit.OrderType = OrderType.Market;
                operation.TakeProfit.SideType = OrderSide.Sell;
                operation.TakeProfit.Price = decimal.Parse(section.GetSection("price").Value);
                operation.TakeProfit.BeGreedy = bool.Parse(greedy);
                operation.TakeProfit.PlainGreed = bool.Parse(plainGreed);
                operation.TakeProfit.Pair = section.GetSection("pair").Exists() ? section.GetSection("pair").Value : Pair;

                if (volume.Exists())
                {
                    operation.TakeProfit.Volume = decimal.Parse(section.GetSection("volume").Value);
                }
                else
                {
                    operation.TakeProfit.Volume ??= operation.StopLoss.Volume;
                }
            }
        }

        private static void SetupStopLossOrder(Operation operation, IConfigurationSection section)
        {
            var order = operation.StopLoss;
            var id = section.GetSection("id");
            var edit = section.GetSection("edit");
            var price = section.GetSection("price");
            var buyPrice = section.GetSection("buyPrice");
            var trailingLevels = section.GetSection("trailing");
            
            if (id.Exists())
            {
                order.Id = id.Value.Trim();
                order.IsPlaced = true;
            }
            else
            {
                order.OrderType = OrderType.StopLoss;
                order.SideType = OrderSide.Sell;
                order.Price = decimal.Parse(price.Value);
                order.Volume = decimal.Parse(section.GetSection("volume").Value);
                order.Pair = section.GetSection("pair").Exists() ? section.GetSection("pair").Value : Configuration.Pair;
            }

            if (edit.Exists())
            {
                order.TriggerPrice = decimal.Parse(edit.GetSection("triggerPrice").Value);
                order.NewStoplossPrice = decimal.Parse(edit.GetSection("newPrice").Value);
            }

            if (trailingLevels.Exists())
            {
                order.TrailingLevels = trailingLevels.GetChildren().Select(t => decimal.Parse(t.Value)).ToList();
            }
        }
    }
}
