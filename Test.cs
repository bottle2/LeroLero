using System;
using System.Reflection;

internal struct OwlExportAttribute { }

internal class Program
{
    static void Main(string[] _)
    {
        var a = new PizzaMushroom();

        Console.WriteLine("Hello, world!");
    }
}

internal abstract class Topping { }

internal abstract class ToppingVeggie : Topping { }

internal abstract class ToppingCheese : ToppingVeggie { }

internal class Mushroom : ToppingVeggie { }

internal class Tomato : ToppingVeggie { }

internal class Mozzarella : ToppingCheese { }

internal abstract class Pizza
{
    internal Topping[] toppings;
}

internal abstract class PizzaVeggie : Pizza { }

internal class Margherita : PizzaVeggie
{
    internal Margherita()
    {
        toppings = [new Mozzarella(), new Tomato()];
    }
}

internal class PizzaMushroom : Pizza
{
    internal PizzaMushroom()
    {
        toppings = [new Mozzarella(), new Mushroom()];
    }
}
