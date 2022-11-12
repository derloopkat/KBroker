using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KBroker
{
    public abstract class Trigger
    {
        public abstract void Execute(Broker broker);
        public bool TasksCompleted { get; set; }
        public Order TakeProfit { get; set; }
        public Order StopLoss { get; set; }
        public decimal? StartPrice { get; set; }
        public dynamic SetupOrders(Broker broker)
        {
            dynamic response = null;
            try
            {
                if (broker is Simulator)
                {
                    KrakenApi.ClearApiKeys();
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
