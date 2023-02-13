using System;
using System.Collections.Generic;
using System.Threading;

namespace KBroker
{
    class Program
    {
        public static DateTime FirstRun;

        static void Main(string[] args)
        {
            try
            {
                Program.FirstRun = DateTime.Now;
                Display.PrintCredits();
                var operation = Configuration.LoadOrders();
                var stopLossWasAddedByUser = operation.StopLoss.IsPlaced;
                Broker broker;
                
                if (operation.Simulation != null)
                {
                    broker = new Simulator
                    (
                        currentPrice: operation.Simulation?.CurrentPrice ?? 0,
                        priceTrend: operation.Simulation?.PriceTrend ?? 0,
                        stopLossPrice: operation.StopLoss.Price
                    );
                }
                else
                {
                    Configuration.LoadSecretKeys();
                    broker = new Broker();
                };

                broker.WaitForStartPrice(operation.StartPrice ?? 0);

                var response = operation.SetupOrders(broker);
                if (!operation.StopLoss.Error)
                {
                    Display.PrintHeader(broker, operation);
                }
                else
                    Display.Print(response);

                if (!stopLossWasAddedByUser) Display.AddOrderResponse(operation.StopLoss, response);

                if (broker is not Simulator && broker.GetSystemStatus() != Broker.SystemStatus.Online)
                {
                    throw new SystemStatusException("Exchange unavailable or under maintenance.");
                }

                broker.Trade(operation);

                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Display.Print(ex.Message, ConsoleColor.Red);
                Logger.AddEntry($"{ex.Message}\r\n{ex.StackTrace}\r\n{ex.InnerException?.Message ?? string.Empty}");
            }
        }
    }
}
