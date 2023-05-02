using KBroker;

Configuration.LoadSecretKeys();
var broker = new Broker();
var response = broker.ClosedOrders(28);
Display.Print(response);