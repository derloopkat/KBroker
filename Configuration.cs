using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;

namespace KBroker
{
    public class Configuration
    {
        public static int Timeout;
        public static float IntervalSeconds;
        public static string Pair;
        public static Trigger Trigger;

        public static Trigger LoadSettings()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("settings.json", optional: false, reloadOnChange: true);
            var root = builder.Build();
            var orderSection = root.GetSection("order");
            var triggerType = orderSection.GetSection("type").Value;
            var startPrice = orderSection.GetSection("startPrice");
            Timeout = int.Parse(root.GetSection("timeout").Value);
            IntervalSeconds = float.Parse(root.GetSection("interval").Value);
            Pair = orderSection.GetSection("pair").Value;
            if (triggerType == "OneCancelsTheOther")
            {
                var trigger = new OneCancelsTheOther();
                SetupStopLossOrder(trigger, orderSection.GetSection("stoploss"));
                SetupTakeProfitOrder(trigger, orderSection.GetSection("takeprofit"));
                Trigger = trigger;
            }
            else if(triggerType == "TrailingStopLoss")
            {
                var trigger = new TrailingStopLoss();
                SetupStopLossOrder(trigger, orderSection.GetSection("stoploss"));
                Trigger = trigger;
            }

            if (startPrice.Exists())
            {
                Trigger.StartPrice = decimal.Parse(startPrice.Value);
            }
            LoadKeys();
            return Trigger;
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

        private static void SetupTakeProfitOrder(Trigger trigger, IConfiguration section)
        {
            var id = section.GetSection("id");
            if (id.Exists())
            {
                trigger.TakeProfit.Id = id.Value;
            }
            else
            {
                var volume = section.GetSection("volume");
                var greedy = section.GetSection("greedy")?.Value ?? "false";
                var plainGreed = section.GetSection("plainGreed")?.Value ?? "false";
                trigger.TakeProfit.OrderType = OrderType.Market;
                trigger.TakeProfit.SideType = OrderSide.Sell;
                trigger.TakeProfit.Price = decimal.Parse(section.GetSection("price").Value);
                trigger.TakeProfit.BeGreedy = bool.Parse(greedy);
                trigger.TakeProfit.PlainGreed = bool.Parse(plainGreed);
                trigger.TakeProfit.Pair = section.GetSection("pair").Exists() ? section.GetSection("pair").Value : Pair;

                if (volume.Exists())
                {
                    trigger.TakeProfit.Volume = decimal.Parse(section.GetSection("volume").Value);
                }
                else
                {
                    trigger.TakeProfit.Volume ??= trigger.StopLoss.Volume;
                }
            }
        }

        private static void SetupStopLossOrder(Trigger trigger, IConfigurationSection section)
        {
            var order = trigger.StopLoss;
            var id = section.GetSection("id");
            var edit = section.GetSection("edit");
            var price = section.GetSection("price");
            var buyPrice = section.GetSection("buyPrice");
            var trailingLevels = section.GetSection("trailing");
            
            if (id.Exists())
            {
                order.Id = id.Value;
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
