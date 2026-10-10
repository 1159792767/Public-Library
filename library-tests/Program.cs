using System;
using Demo.Common;
if(PriceCalculator.Total(10m,3)!=30m)throw new Exception("Price multiplication failed");
bool rejected=false;try{PriceCalculator.Total(10m,-1);}catch(ArgumentOutOfRangeException){rejected=true;}
if(!rejected)throw new Exception("Negative quantity must be rejected");
Console.WriteLine("PASS multiplication and negative quantity guard");
