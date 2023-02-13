using KBroker;

Configuration.LoadKeys();
var broker = new Broker();
var order = new Order();
Configuration.Pair = "LRCUSD";
order.SideType = OrderSide.Sell;
order.OrderType = OrderType.StopLoss;
order.Volume = 100M;
order.Price = 0.01M;
order.TriggerBy = OrderTriggerBy.Index;
order.Validate = true;
var response = broker.AddOrder(order);
Display.Print(response);
