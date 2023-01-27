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
            Program.FirstRun = DateTime.Now;
            Display.PrintCredits();
            var operation = Configuration.LoadOrders();
            var stopLossWasAddedByUser = operation.StopLoss.IsPlaced;

            ///* Comment or uncomment the following code */
            ///* This line is for running simulation with fake broker */
            var broker = new Simulator
            (
                currentPrice: 15.9m,
                priceTrend: SimulatedPriceTrend.MockedAscending,
                stopLossPrice: 13.727m
            );

            ///* Below two lines are running the application for real  */
            //Configuration.LoadSecretKeys();
            //var broker = new Broker();

            broker.WaitForStartPrice(broker.Pair, operation.StartPrice ?? 0);

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
    }
}
