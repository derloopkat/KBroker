using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KBroker
{
    public class TrailingStopLoss : Operation
    {
        public TrailingStopLoss()
        {
            StopLoss = new Order();
        }

        public override void Execute(Broker broker)
        {
            var stopLossPriceWhenExecuteCalled = StopLoss.Price;
            try
            {
                var lastPrice = broker.LastPriceId == 0 ? null : broker.Prices[broker.LastPriceId];
                var price = broker.GetCurrentPrice();
                Display.PrintProgress(
                    StopLoss.TriggerTrailingPrice ?? StopLoss.NextTrailingPrice ?? StopLoss.Price.Value, 
                    StopLoss.Price.Value, 
                    price.Close, 
                    lastPrice?.Close ?? price.Close);

                var response = broker.QueryOrder(StopLoss);
                if (StopLoss.IsClosed && StopLoss.IsCompleted)
                {
                    Display.Print(response);
                    Display.PrintSuccess($"Stoploss order was triggered by price. Received: ${StopLoss.Cost}.");
                    TasksCompleted = true;
                }
                else if (StopLoss.IsCanceled)
                {
                    Display.PrintError($"Stoploss order was canceled by user.");
                    TasksCompleted = true;
                }
                else if (price.Close >= StopLoss.TriggerTrailingPrice)
                {
                    if (broker.GetSystemStatus() != Broker.SystemStatus.Online)
                    {
                        throw new SystemStatusException("Exchange unavailable or under maintenance.");
                    }

                    StopLoss.Price = StopLoss.NextTrailingPrice;
                    response = broker.EditOrder(StopLoss);
                    StopLoss.TrailingLevels.RemoveAt(0);
                    Display.AddOrderResponse(StopLoss, response);                    
                }
                if(StopLoss.TrailingLevels.Count == 2)
                {
                    var lastDifference = StopLoss.TriggerTrailingPrice.Value - StopLoss.NextTrailingPrice.Value;
                    StopLoss.TrailingLevels.Add(StopLoss.TriggerTrailingPrice.Value + lastDifference);
                }
            }
            catch (SystemStatusException ex)
            {
                Display.PrintError(ex.Message, false);
                Logger.AddEntry(ex.Message);
                Thread.Sleep(20000);
            }
            catch (Exception ex)
            {
                StopLoss.Price = stopLossPriceWhenExecuteCalled;
                Logger.AddEntry(ex.Message);
                Display.PrintError(ex.Message);
            }
        }
        
        public bool LevelsAreConsistent()
        {
            var sorted = StopLoss.TrailingLevels
                .OrderBy(p => p)
                .Select((price, index) => new { price, index });
            return !sorted.Any(level => StopLoss.TrailingLevels[level.index] != level.price);
        }
    }
}
