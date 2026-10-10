namespace Demo.Common;
public static class PriceCalculator {
 public static decimal Total(decimal price, int quantity) {
  if(quantity<0)throw new System.ArgumentOutOfRangeException(nameof(quantity),"Quantity cannot be negative");
  return price * quantity;
 }
}

