using System.Collections;
using System.Reflection;
using VDS.RDF;
using VDS.RDF.Ontology;
using VDS.RDF.Writing;

internal struct OwlExportAttribute { }

internal class Program
{
    private static OntologyGraph g;
    static void Main()
    {
        Console.WriteLine("Hello, world!");

        g = new OntologyGraph(UriFactory.Create("http://pizza.com"));
        g.NamespaceMap.AddNamespace("pizza", UriFactory.Create("http://pizza.com"));

        _ = Traverse(new PizzaMushroom());
        _ = Traverse(new Margherita());

        new RdfXmlWriter().Save(g, "pizza.rdf");
    }

    static Individual Traverse(object any)
    {
        Type leType = any.GetType();

        OntologyClass previous = g.CreateOntologyClass(g.CreateUriNode($"pizza:{leType.Name}"));
        var individual = g.CreateIndividual(g.CreateUriNode($"pizza:{leType.Name}Individual"), previous.Resource);

        foreach (var hey in leType.FindMembers(MemberTypes.Field, BindingFlags.Instance | BindingFlags.NonPublic, (m, fc) => true, null))
        {
            Console.WriteLine(hey);
            var valor = ((FieldInfo)hey).GetValue(any);
            if (valor!.GetType().IsAssignableTo(typeof(IEnumerable)))
            {
                Console.WriteLine("is some collection");
                foreach (object obj in (IEnumerable)valor)
                {
                    // TODO
                    // Precisa criar uma propriedade, aí faz um assert que não é assert entre dois
                    // individuos.

                    //Individual other = Traverse(obj);
                    //var prop = g.CreateOntologyProperty(g.CreateUriNode($"pizza:{hey.Name}"));
                    //prop.add
                    //g.Assert(individual.Resource, prop.Resource, other.Resource);
                    //individual.AddResourceProperty($"pizza:{hey.Name}", other.Resource, true);
                }
            }
        }

        for (Type current = leType.BaseType!; current != null && current != typeof(object); current = current.BaseType!)
        {
            OntologyClass lala = g.CreateOntologyClass(g.CreateUriNode($"pizza:{current.Name}"));
            previous.AddSuperClass(lala);
            previous = lala;
        }

        return individual;
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
