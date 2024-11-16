// XXX Coisas parecidas?
// https://github.com/dotnetrdf/dotnetrdf/discussions/594
// - https://github.com/giacomociti/iride
// - https://github.com/EcoStruxure/OLGA
// - https://github.com/ukparliament/Mapping

internal class Program
{
    static void Main()
    {
        OwlGenerator owl = new OwlGenerator("http://pizza.com");
        owl.AddIndividual(new PizzaMushroom(), "mushroomPizza");
        owl.AddIndividual(new Margherita(), "margheritaPizza");
        owl.Render("pizza");
    }
}

internal abstract class Topping { }

internal abstract class ToppingVeggie : Topping { }

internal abstract class ToppingCheese : Topping { }

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
