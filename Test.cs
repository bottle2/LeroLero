// XXX Coisas parecidas?
// https://github.com/dotnetrdf/dotnetrdf/discussions/594
// - https://github.com/giacomociti/iride
// - https://github.com/EcoStruxure/OLGA
// - https://github.com/ukparliament/Mapping

internal class Program
{
    static void Main()
    {
        OwlGenerator owl = new OwlGenerator("http://pizza.com", "A ontology for Pizza");
        owl.AddIndividual(new PizzaMushroom(), "mushroomPizza");
        owl.AddIndividual(new Margherita(), "margheritaPizza");
        owl.Render("pizza");
    }
}

internal abstract class Topping { }

internal abstract class ToppingVeggie : Topping { }

[OwlDisjointWith(typeof(ToppingVeggie))]
internal abstract class ToppingCheese : Topping { }

internal class Mushroom : ToppingVeggie { }

[OwlDisjointWith(typeof(Mushroom))]
internal class Tomato : ToppingVeggie { }

internal class Mozzarella : ToppingCheese { }

[OwlDisjointWith(typeof(Topping))]
internal abstract class Pizza
{
    internal Topping[] toppings;
}

internal abstract class PizzaVeggie : Pizza { }

[OwlComment("A pizza that has Mozzarella and Tomato toppings")]
internal class Margherita : PizzaVeggie
{
    internal Margherita()
    {
        toppings = [new Mozzarella(), new Tomato()];
    }
}

[OwlDisjointWith(typeof(PizzaVeggie))]
internal class PizzaMushroom : Pizza
{
    internal PizzaMushroom()
    {
        toppings = [new Mozzarella(), new Mushroom()];
    }
}
