using System;
using System.Reflection;
using Sustem.Type;

struct OwlExportAttribute { }

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, world!");
    }
}

abstract class Topping { }

abstract class ToppingVeggie : Topping { }

abstract class ToppingCheese : ToppingVeggie { }

class Mushroom : ToppingVeggie { }

class Tomato : ToppingVeggie { }

class Mozzarella : ToppingCheese { }

abstract class Pizza
{
    internal Topping[] toppings;
}

abstract class PizzaVeggie : Pizza { }

class Margherita : PizzaVeggie
{
    Margherita()
    {
        toppings = new Topping[]{ new Mozzarella(), new Tomato() };
    }
}

class PizzaMushroom : Pizza
{
    PizzaMushroom()
    {
        toppings = new Topping[]{ new Mozzarella(), new Mushroom() };
    }
}
