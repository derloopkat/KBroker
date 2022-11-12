using KBroker;

var broker = new Simulator
(
    currentPrice: (decimal)14.5,
    priceTrend: SimulatedPriceTrend.MockedAscending,
    stopLossPrice: 14
);