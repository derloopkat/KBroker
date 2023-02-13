using KBroker;
using System.Threading;

Configuration.LoadKeys();
var operation = Configuration.LoadOrders();
var broker = new Simulator(87, SimulatedPriceTrend.MockedAscending);
var intervalMiliseconds = 10000;
operation.SetupOrders(broker);
Display.PrintHeader(broker, operation);

while (!operation.TasksCompleted)
{
    operation.Execute(broker);
    Thread.Sleep(intervalMiliseconds);
}

var order = new Order("XXXXXX-XXXXX-XXXX");
Configuration.Pair = "FOOUSD";
order.Price = 20;
var response = broker.EditOrder(order);
//var response = broker.QueryOrder(order);
Display.Print(response);
