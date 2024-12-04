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
        owl.AddIndividual(new Person(19, 1.77f, "Paula Tejante", true, BloodType.A_PLUS));
        owl.AddIndividual(new Person(22, 1.65f, "Thomas Turbando", true, BloodType.O_MINUS));
        owl.AddIndividual(new Person(21, 1.80f, "Cuca Beludo", false, BloodType.AB_MINUS));
        owl.AddIndividual(new Person(33, 1.59f, "Zeca Gado", false, BloodType.B_PLUS));
        owl.AddIndividual(new Person(55, 1.69f, "Oscar Alho", true, BloodType.AB_PLUS));
        owl.AddIndividual(new Person(38, 1.91f, "Paula Noku", true, BloodType.A_PLUS));
        owl.AddIndividual(new Person(70, 1.58f, "Jacinto Aquino Rego", false, BloodType.AB_PLUS));
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

internal enum BloodType { A_PLUS, A_MINUS, B_PLUS, B_MINUS, AB_PLUS, AB_MINUS, O_PLUS, O_MINUS };

internal record Person(
    int Age,
    float Height,
    [field: OwlIndividualName]
    string Name,
    bool WantsToDie,
    BloodType BloodType
);
