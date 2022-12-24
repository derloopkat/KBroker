using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KBroker
{
    public abstract class Operation
    {
        public abstract void Execute(Broker broker);
        public bool TasksCompleted { get; set; }
        public bool UseMarketPrice { get; set; }
        public Order TakeProfit { get; set; }
        public Order StopLoss { get; set; }
        public decimal? StartPrice { get; set; }
        public float? Version { get; set; }
        public string[] CancelOrders { get; set; }
        public dynamic SetupOrders(Broker broker)
        {
            dynamic response = null;
            try
            {
                if (broker is Simulator)
                {
                    KrakenApi.ClearApiKeys();
                }

                if(CancelOrders != null)
                {
                    foreach (var id in CancelOrders)
                    {
                        var order = new Order(id);
                        response = broker.CancelOrder(order);
                        if (order.Error)
                            Display.Print($"Unable to cancel order \"{order.Id}\".");
                        else
                            Display.PrintSuccess($"Order \"{order.Id}\" cancelled");
                    }
                    Display.Print(Environment.NewLine);
                }

                if (StopLoss.IsPlaced)
                {
                    response = broker.QueryOrder(StopLoss);
                    if (StopLoss.IsOpen)
                    {
                        StopLoss.Price = decimal.Parse(response["result"][StopLoss.Id]["descr"]?["price"].Value);
                        StopLoss.Pair = response["result"]?[StopLoss.Id]?["descr"]?["pair"].Value;
                        StopLoss.Volume = decimal.Parse(response["result"][StopLoss.Id]["vol"].Value);
                    }
                    else
                    {
                        Display.PrintError("Unable to find an open order with the specified number. The order might have been completed or cancelled by user.");
                        StopLoss.Error = true;
                    }
                }
                else if(StopLoss.Price.HasValue)
                {
                    response = broker.AddOrder(StopLoss);
                    if (StopLoss.Error)
                    {
                        Display.PrintError("Unable add stop loss order.");
                        StopLoss.Error = true;
                    }
                }
            }
            catch (Exception ex)
            {                
                Display.PrintError("Unable to set up order. " +  ex.Message);
                StopLoss.Error = true;
            }
            finally
            {
                if (StopLoss.Error)
                {
                    TasksCompleted = true;
                }
            }
            return response;
        }
    }
}
